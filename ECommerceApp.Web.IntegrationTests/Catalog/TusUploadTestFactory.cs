using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Catalog.Products.ViewModels;
using ECommerceApp.Application.FileManager;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Shared.TestInfrastructure;
using ECommerceApp.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Web.IntegrationTests.Catalog
{
    /// <summary>
    /// Web application factory for TUS chunked upload tests.
    ///
    /// Configuration overrides:
    ///  - Catalog:ChunkedUploadImplementation = "TUS"  (enables tusdotnet middleware)
    ///  - IFileStore → TusFakeFileStore               (no disk I/O during tests)
    ///  - ICategoryService → TusNullCategoryService   (required: _Layout always calls GetAllCategories)
    /// </summary>
    public sealed class TusUploadTestFactory : CustomWebApplicationFactory<Startup>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            // Inject the TUS feature flag on top of the test appsettings.
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Catalog:ChunkedUploadImplementation"] = "TUS"
                });
            });
        }

        protected override void OverrideServicesImplementation(IServiceCollection services)
        {
            // Prevent disk writes during tests.
            var fileStoreDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IFileStore));
            if (fileStoreDescriptor != null) services.Remove(fileStoreDescriptor);
            services.AddSingleton<IFileStore, TusFakeFileStore>();

            // _Layout.cshtml always calls ICategoryService.GetAllCategories().
            var categoryDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICategoryService));
            if (categoryDescriptor != null) services.Remove(categoryDescriptor);
            services.AddScoped<ICategoryService, TusNullCategoryService>();
        }
    }

    internal sealed class TusNullCategoryService : ICategoryService
    {
        public Task<int> AddCategory(CreateCategoryDto dto) => Task.FromResult(0);
        public Task<bool> UpdateCategory(UpdateCategoryDto dto) => Task.FromResult(false);
        public Task<bool> DeleteCategory(int id) => Task.FromResult(false);
        public Task<CategoryVm> GetCategory(int id) => Task.FromResult<CategoryVm>(null!);
        public Task<List<CategoryVm>> GetAllCategories() => Task.FromResult(new List<CategoryVm>());
    }

    /// <summary>
    /// No-op IFileStore: satisfies the interface without touching disk.
    /// </summary>
    internal sealed class TusFakeFileStore : IFileStore
    {
        private static readonly byte[] _jpeg = { 0xFF, 0xD8, 0xFF, 0xE0 };

        public byte[] ReadFile(string path) => _jpeg;
        public Task<byte[]> ReadFileAsync(string path) => Task.FromResult(_jpeg);
        public Task<FileDirectoryPOCO> WriteFileAsync(IFormFile file)
            => WriteFileAsync(file, Path.Combine(Path.GetTempPath(), "tus-test"));
        public Task<FileDirectoryPOCO> WriteFileAsync(IFormFile file, string path)
            => Task.FromResult(new FileDirectoryPOCO { SourcePath = Path.Combine(path, $"{Guid.NewGuid()}.jpg"), Name = "test.jpg" });
        public FileDirectoryPOCO WriteFile(string fileName, byte[] file, string path)
            => new() { SourcePath = Path.Combine(path, fileName), Name = Path.GetFileName(fileName) };
        public string SafeWriteFile(byte[] content, string sourceFileName, string path)
            => Path.Combine(path, sourceFileName);
        public ICollection<FileDirectoryPOCO> WriteFiles(ICollection<IFormFile> files, string path)
            => files.Select(f => new FileDirectoryPOCO { SourcePath = Path.Combine(path, $"{Guid.NewGuid()}.jpg"), Name = "test.jpg" }).ToList();
        public string GetFileExtenstion(string file) => Path.GetExtension(file);
        public string ReplaceInvalidChars(string fileName)
            => string.Join("", fileName.Split(Path.GetInvalidFileNameChars()));
        public byte[] GetFilesPackedIntoZip(ICollection<FileDirectoryPOCO> fileDirectories) => Array.Empty<byte>();
        public void DeleteFile(string path) { }
    }
}
