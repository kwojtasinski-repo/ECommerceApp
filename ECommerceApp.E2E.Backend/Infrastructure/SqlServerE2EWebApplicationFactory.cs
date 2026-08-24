using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Shared.TestInfrastructure;
using ECommerceApp.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.E2E.Backend.Infrastructure
{
    /// <summary>
    /// WebApplicationFactory for real-infrastructure E2E tests. Unlike the regular integration
    /// suite's <c>BcWebApplicationFactory</c> (which swaps every BC DbContext to EF Core's
    /// InMemory provider), this factory does not touch DbContext registrations at all: it only
    /// overrides <c>ConnectionStrings:DefaultConnection</c> to point at an ephemeral SQL Server
    /// container (see <see cref="MsSqlE2EFixture"/>) and forces <c>Database:RunMigrationsOnStart</c>.
    /// <para>
    /// This means the production DI graph (<c>Startup.ConfigureServices</c> → <c>AddInfrastructure</c>)
    /// runs unmodified: every bounded context's real <c>IDbContextMigrator</c> executes against a real
    /// SQL Server engine at host startup, exactly as it does in production. That real-engine behavior —
    /// not EF Core's InMemory provider — is what makes tests built on this factory E2E rather than
    /// regular integration tests.
    /// </para>
    /// <para>
    /// <see cref="IMessageBroker"/> is still swapped to <see cref="SynchronousMultiHandlerBroker"/>, same
    /// as <c>BcWebApplicationFactory</c>. This is orthogonal to the real-vs-InMemory database distinction.
    /// <see cref="IModuleClient"/> is likewise swapped to <see cref="PerHandlerScopeModuleClient"/>: production's
    /// <c>ModuleClient.PublishAsync</c> already resolves and invokes every registered handler (fixed
    /// under KI-007), but does so against one shared ambient DI scope. Against a real SQL Server engine
    /// with the background outbox dispatcher running, giving every handler its own DI scope — and thus
    /// its own <c>DbContext</c> — avoids the same-context nested-transaction conflicts a shared scope
    /// would risk between handlers reacting to the same message.
    /// </para>
    /// </summary>
    public class SqlServerE2EWebApplicationFactory : WebApplicationFactory<Startup>
    {
        private readonly string _connectionString;

        public SqlServerE2EWebApplicationFactory(string connectionString)
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
                    ["MessagingOptions:UseBackgroundDispatcher"] = "true",
                    ["MessagingOptions:OutboxPollInterval"] = "00:00:00.100",
                });
            });

            builder.ConfigureServices(services =>
            {
                var brokerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMessageBroker));
                if (brokerDescriptor != null)
                    services.Remove(brokerDescriptor);

                services.AddScoped<IMessageBroker, SynchronousMultiHandlerBroker>();

                foreach (var moduleClientDescriptor in services
                    .Where(d => d.ServiceType == typeof(IModuleClient))
                    .ToList())
                {
                    services.Remove(moduleClientDescriptor);
                }

                services.AddScoped<IModuleClient, PerHandlerScopeModuleClient>();
            });

            builder.UseEnvironment("test");
        }
    }
}
