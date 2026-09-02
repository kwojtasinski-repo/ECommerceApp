using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Catalog.Products.ViewModels;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Identity.IAM;
using ECommerceApp.Shared.TestInfrastructure;
using ECommerceApp.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.Infrastructure
{
    /// <summary>
    /// Hosts <see cref="ECommerceApp.Web"/> for Playwright to drive over real HTTP.
    /// <para>
    /// <b>Do not use the inherited <see cref="Services"/>, <see cref="CreateClient()"/>, or
    /// <see cref="Server"/> members on this class.</b> In .NET 10's
    /// <c>Microsoft.AspNetCore.Mvc.Testing</c>, <c>WebApplicationFactory.StartServer()</c>
    /// unconditionally casts the built host's server to <c>TestServer</c> — it cannot be redirected to
    /// a real Kestrel-bound host via a <c>CreateHost</c> override (verified empirically: overriding
    /// <c>CreateHost</c> to call <c>UseKestrel()</c> throws <c>InvalidCastException</c> from
    /// <c>WebApplicationFactory.StartServer()</c>). Touching those inherited members lazily builds a
    /// second, completely independent TestServer-backed host with its own InMemory database — one that
    /// Playwright never talks to. Always go through <see cref="StartKestrelHost"/> and
    /// <see cref="ServerAddress"/> instead.
    /// </para>
    /// </summary>
    public sealed class PlaywrightWebApplicationFactory : CustomWebApplicationFactory<Startup>
    {
        private readonly string _connectionString;
        private IHost _kestrelHost;

        public PlaywrightWebApplicationFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public PlaywrightWebApplicationFactory()
            : this(PlaywrightSqlFixture.ConnectionString ?? throw new InvalidOperationException(
                "PlaywrightSqlFixture must be initialized before creating a Playwright web application factory."))
        {
        }

        public string ServerAddress { get; private set; } = default!;

        /// <summary>
        /// Runs inside the base factory's own <c>ConfigureServices</c> callback, before it builds its
        /// provider and seeds IAM. That ordering matters: doing this from a *second*
        /// <c>ConfigureServices</c> callback — which runs after the base has already seeded — would leave
        /// the seeded users (including <c>test@test</c>) stranded in a store the running host never opens.
        /// Bounded-context DbContexts are left pointed at the real connection string passed to this
        /// factory (phase 22) — only <see cref="IamDbContext"/> is re-pointed at a fresh InMemory store
        /// below, for the reason explained there.
        /// </summary>
        protected override void OverrideServicesImplementation(IServiceCollection services)
        {
            // The base factory points IamDbContext at a fixed InMemory database name
            // ("InMemoryIamDatabase"), which EF Core's InMemory provider treats as one store shared by
            // every DbContext in the process using that name. IAM isn't part of this phase's SQL Server
            // migration, so it stays on InMemory — but each factory needs its own store, otherwise two
            // factories seeding the same fixed test users (e2e-admin@example.com etc.) concurrently race
            // on the same underlying change tracker and throw identity-map conflicts.
            services.ReplaceDbContextWithInMemory<IamDbContext>($"InMemoryIamDatabase_{Guid.NewGuid():N}");

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICategoryService));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            foreach (var databaseInitializer in services
                .Where(d => d.ServiceType == typeof(IDatabaseInitializer))
                .ToList())
            {
                services.Remove(databaseInitializer);
            }

            services.AddScoped<IDatabaseInitializer, PlaywrightDatabaseInitializer>();
            services.AddScoped<ICategoryService, NullCategoryService>();
        }

        /// <summary>
        /// Builds and starts a real Kestrel-bound host (dynamic port) running the same DI wiring as
        /// <see cref="ConfigureWebHost"/>, for Playwright — a separate browser process over real HTTP —
        /// to navigate against. Idempotent: subsequent calls return the already-started host's services.
        /// </summary>
        public IServiceProvider StartKestrelHost()
        {
            if (_kestrelHost != null)
            {
                return _kestrelHost.Services;
            }

            var hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webHostBuilder =>
                {
                    webHostBuilder
                        .UseKestrel()
                        .UseUrls("http://127.0.0.1:0")
                            .ConfigureAppConfiguration((_, configuration) =>
                            {
                                configuration.AddInMemoryCollection(new Dictionary<string, string>
                                {
                                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                                    ["Database:RunMigrationsOnStart"] = "true",
                                    // Production default is 10s (MessagingOptions.OutboxPollInterval);
                                    // on the shared E2E database the Outbox accumulates rows from every
                                    // other test class, and a 10s cadence leaves too few poll cycles
                                    // inside a test's timeout to reliably catch its own message. Same
                                    // override SqlServerE2EWebApplicationFactory uses for E2E.Backend.
                                    ["MessagingOptions:OutboxPollInterval"] = "00:00:00.100"
                                });
                            })
                        // ECommerceApp.Web's wwwroot is only physically present next to its own build
                        // output, not copied into this test project's output directory (only its DLL and
                        // a Static Web Assets *manifest* are). JsVersionProvider and other code that reads
                        // IWebHostEnvironment.WebRootPath as a plain string (not the manifest-aware
                        // WebRootFileProvider) need a real physical wwwroot on disk, so content root must
                        // point at ECommerceApp.Web's actual project folder. Found by searching upward for
                        // ECommerceApp.sln rather than assuming a fixed build-output depth (Debug/Release,
                        // publish layout, and CI checkout paths all differ).
                        .UseContentRoot(FindWebProjectContentRoot())
                        // Host.CreateDefaultBuilder() defaults ApplicationName to the running process's
                        // entry assembly (this test project), which breaks MVC's ApplicationPart/compiled-
                        // Razor-view discovery — it looks in ECommerceApp.Web.E2E.dll instead of
                        // ECommerceApp.Web.dll and every view lookup fails (WebApplicationFactory<T>
                        // fixes this internally for its own TestServer host; a hand-rolled Kestrel host
                        // has to do it explicitly).
                        .UseSetting(WebHostDefaults.ApplicationKey, typeof(Startup).Assembly.GetName().Name)
                        .UseStartup<Startup>();

                    ConfigureWebHost(webHostBuilder);
                    webHostBuilder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
                    webHostBuilder.UseEnvironment("Development");
                });

            _kestrelHost = hostBuilder.Build();
            _kestrelHost.Start();
            ServerAddress = _kestrelHost.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .First();

            return _kestrelHost.Services;
        }

        private static string FindWebProjectContentRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ECommerceApp.sln")))
            {
                dir = dir.Parent;
            }

            if (dir == null)
            {
                throw new InvalidOperationException(
                    "Could not locate ECommerceApp.sln by walking up from " + AppContext.BaseDirectory +
                    " — cannot resolve ECommerceApp.Web's content root.");
            }

            return Path.Combine(dir.FullName, "ECommerceApp.Web");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _kestrelHost?.Dispose();
                _kestrelHost = null;
            }

            base.Dispose(disposing);
        }
    }

    internal sealed class PlaywrightDatabaseInitializer : IDatabaseInitializer
    {
        private readonly IEnumerable<IDbContextMigrator> _migrators;

        public PlaywrightDatabaseInitializer(IEnumerable<IDbContextMigrator> migrators)
        {
            _migrators = migrators;
        }

        public async Task Initialize()
        {
            foreach (var migrator in _migrators)
            {
                if (migrator.ContextName == "IamDbContext")
                {
                    continue;
                }

                await migrator.MigrateAsync();
            }
        }
    }

    internal sealed class NullCategoryService : ICategoryService
    {
        public Task<int> AddCategory(CreateCategoryDto dto) => Task.FromResult(0);
        public Task<bool> UpdateCategory(UpdateCategoryDto dto) => Task.FromResult(false);
        public Task<bool> DeleteCategory(int id) => Task.FromResult(false);
        public Task<CategoryVm> GetCategory(int id) => Task.FromResult<CategoryVm>(null!);
        public Task<List<CategoryVm>> GetAllCategories() => Task.FromResult(new List<CategoryVm>());
    }
}
