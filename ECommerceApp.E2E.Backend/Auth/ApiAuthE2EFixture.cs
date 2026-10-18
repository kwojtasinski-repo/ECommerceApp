using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;
using Xunit;

namespace ECommerceApp.E2E.Backend.Auth
{
    /// <summary>
    /// Hosts the real <c>ECommerceApp.API</c> startup on an ephemeral SQL Server container, with every
    /// bounded context's real migrations. Unlike the Web factory, nothing about authentication is replaced,
    /// so tests see exactly the schemes production registers.
    /// </summary>
    public sealed class ApiE2EWebApplicationFactory : WebApplicationFactory<ECommerceApp.API.Startup>
    {
        private readonly string _connectionString;

        public ApiE2EWebApplicationFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(logging => TestLogging.Configure(logging));

            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.test.json"), optional: false, reloadOnChange: false);
                cfg.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                    ["Database:RunMigrationsOnStart"] = "true",
                    ["MessagingOptions:UseBackgroundDispatcher"] = "false",
                });
            });

            builder.UseEnvironment("test");
        }
    }

    /// <summary>
    /// One SQL Server container and one API host for <see cref="ApiAuthE2ETests"/>.
    /// </summary>
    public sealed class ApiAuthE2EFixture : IAsyncLifetime
    {
        public const string CollectionName = "ApiAuthSqlServer";

        // Seeded by the IAM migrations (IamDbContext).
        public const string SeededAdminEmail = "admin@localhost";
        public const string SeededAdminPassword = "aDminN@W25!";

        private readonly MsSqlContainer _container = new MsSqlBuilder()
            .WithLogger(TestLogging.CreateTestcontainersLogger())
            .WithOutputConsumer(TestLogging.CreateContainerOutputConsumer())
            .Build();
        private ApiE2EWebApplicationFactory _factory;

        public ApiE2EWebApplicationFactory Factory => _factory ?? throw new InvalidOperationException(
            $"{nameof(ApiAuthE2EFixture)} has not been initialized yet.");

        /// <summary>A client that never stores or sends cookies, so only what a test sets explicitly reaches the API.</summary>
        public HttpClient CreateCookielessClient()
            => Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });

        public async ValueTask InitializeAsync()
        {
            await _container.StartAsync();
            _factory = new ApiE2EWebApplicationFactory(_container.GetConnectionString());

            // Building the host runs every context's real migrations once, up front.
            _ = _factory.Services;
        }

        public async ValueTask DisposeAsync()
        {
            if (_factory != null)
            {
                await _factory.DisposeAsync();
            }

            await _container.DisposeAsync();
        }
    }

    [CollectionDefinition(ApiAuthE2EFixture.CollectionName)]
    public sealed class ApiAuthSqlServerCollection : ICollectionFixture<ApiAuthE2EFixture>
    {
    }
}
