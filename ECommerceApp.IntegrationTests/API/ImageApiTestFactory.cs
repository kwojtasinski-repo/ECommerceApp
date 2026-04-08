using ECommerceApp.API;
using ECommerceApp.Application.FileManager;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Identity.IAM;
using ECommerceApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.IntegrationTests.API
{
    public sealed class ImageApiTestFactory : CustomWebApplicationFactory<Startup>
    {
        private bool _seeded;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                // Replace all per-BC DbContextOptions with in-memory databases
                var bcContextOptions = services
                    .Where(d => d.ServiceType.IsGenericType
                        && d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)
                        && d.ServiceType != typeof(DbContextOptions<Context>)
                        && d.ServiceType != typeof(DbContextOptions<IamDbContext>))
                    .ToList();

                foreach (var descriptor in bcContextOptions)
                {
                    var dbContextType = descriptor.ServiceType.GetGenericArguments()[0];
                    var dbName = $"ImageApiTest_{dbContextType.Name}_{Guid.NewGuid():N}";
                    services.Remove(descriptor);

                    var optionsBuilderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(dbContextType);
                    var optionsBuilder = (DbContextOptionsBuilder)Activator.CreateInstance(optionsBuilderType)!;
                    optionsBuilder.UseInMemoryDatabase(dbName)
                        .ReplaceService<IValueGeneratorSelector, TypedIdAwareValueGeneratorSelector>();
                    services.AddSingleton(descriptor.ServiceType, optionsBuilder.Options);
                }

                // Make all scoped BC services transient to avoid EF change-tracker conflicts
                var scopedDescriptors = services
                    .Where(d => d.Lifetime == ServiceLifetime.Scoped
                        && d.ServiceType != typeof(Context)
                        && d.ServiceType != typeof(IamDbContext))
                    .ToList();

                foreach (var descriptor in scopedDescriptors)
                {
                    services.Remove(descriptor);
                    if (descriptor.ImplementationFactory != null)
                        services.Add(new ServiceDescriptor(descriptor.ServiceType, descriptor.ImplementationFactory, ServiceLifetime.Transient));
                    else
                        services.Add(new ServiceDescriptor(descriptor.ServiceType, descriptor.ImplementationType ?? descriptor.ServiceType, ServiceLifetime.Transient));
                }

                // Remove background message dispatcher to avoid interference with tests
                var backgroundDispatchers = services
                    .Where(d => d.ServiceType == typeof(IHostedService)
                        && d.ImplementationType?.Name == "BackgroundMessageDispatcher")
                    .ToList();
                foreach (var d in backgroundDispatchers) services.Remove(d);

                // NoOp migrators — InMemory databases don't support migrations
                var migrators = services.Where(d => d.ServiceType == typeof(IDbContextMigrator)).ToList();
                foreach (var d in migrators) services.Remove(d);
                services.AddScoped<IDbContextMigrator, NoOpDbContextMigrator>();
            });
        }

        protected override void OverrideServicesImplementation(IServiceCollection services)
        {
            var fileStoreDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IFileStore));
            if (fileStoreDescriptor != null) services.Remove(fileStoreDescriptor);
            services.AddSingleton<IFileStore, FakeFileStore>();
        }

        public async Task EnsureSeedCatalogData()
        {
            if (_seeded) 
            {
                return;
            }
            _seeded = true;

            using var scope = Services.CreateScope();
            var sp = scope.ServiceProvider;

            var categoryRepo = sp.GetRequiredService<ICategoryRepository>();
            var productRepo = sp.GetRequiredService<IProductRepository>();

            var category = Category.Create("Test Category");
            var categoryId = await categoryRepo.AddAsync(category);

            var product = Product.Create("Test Product", 100m, "Test description", categoryId.Value);
            await productRepo.AddAsync(product);
        }
    }

    internal sealed class FakeFileStore : IFileStore
    {
        private static readonly byte[] _testBytes = { 0xFF, 0xD8, 0xFF, 0xE0 };

        public byte[] ReadFile(string path) => _testBytes;
        
        public Task<byte[]> ReadFileAsync(string path) => Task.FromResult(_testBytes);

        public Task<FileDirectoryPOCO> WriteFileAsync(IFormFile file)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "Upload", "Images");
            return WriteFileAsync(file, path);
        }

        public Task<FileDirectoryPOCO> WriteFileAsync(IFormFile file, string path)
        {
            var sourcePath = Path.Combine(path, $"{Guid.NewGuid()}_test.jpg");
            return Task.FromResult(new FileDirectoryPOCO { SourcePath = sourcePath, Name = "test.jpg" });
        }

        public FileDirectoryPOCO WriteFile(string fileName, byte[] file, string path) =>
            new FileDirectoryPOCO { SourcePath = Path.Combine(path, fileName), Name = Path.GetFileName(fileName) };

        public string SafeWriteFile(byte[] content, string sourceFileName, string path) =>
            Path.Combine(path, sourceFileName);

        public ICollection<FileDirectoryPOCO> WriteFiles(ICollection<IFormFile> files, string path)
        {
            var path2 = path;
            return files.Select(f =>
            {
                var sourcePath = Path.Combine(path2, $"{Guid.NewGuid()}_test.jpg");
                return new FileDirectoryPOCO { SourcePath = sourcePath, Name = "test.jpg" };
            }).ToList();
        }

        public string GetFileExtenstion(string file) => Path.GetExtension(file);

        public string ReplaceInvalidChars(string fileName) =>
            string.Join("", fileName.Split(Path.GetInvalidFileNameChars()));

        public byte[] GetFilesPackedIntoZip(ICollection<FileDirectoryPOCO> fileDirectories) =>
            Array.Empty<byte>();

        public void DeleteFile(string path) { }
    }
}
