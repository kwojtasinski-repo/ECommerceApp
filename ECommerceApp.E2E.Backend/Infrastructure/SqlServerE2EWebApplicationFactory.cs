using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Shared.TestInfrastructure;
using ECommerceApp.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    /// as <c>BcWebApplicationFactory</c>. This is orthogonal to the real-vs-InMemory database distinction:
    /// production's default <c>ModuleClient</c> dispatches via <c>dynamic</c>, which cannot bind to the
    /// (deliberately <c>internal</c>) handler classes from outside their declaring assembly, and only ever
    /// calls the single first-registered handler besides. Every existing test in this codebase already
    /// works around both issues the same way; this factory keeps that convention for deterministic,
    /// multi-consumer-safe dispatch instead of re-introducing a known dispatcher limitation.
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

                services.AddScoped<IModuleClient, IsolatedModuleClient>();
            });

            builder.UseEnvironment("test");
        }

        private sealed class IsolatedModuleClient : IModuleClient
        {
            private readonly IServiceScopeFactory _scopeFactory;
            private readonly ILogger<IsolatedModuleClient> _logger;

            public IsolatedModuleClient(IServiceScopeFactory scopeFactory, ILogger<IsolatedModuleClient> logger)
            {
                _scopeFactory = scopeFactory;
                _logger = logger;
            }

            public async Task PublishAsync(IMessage message, long? outboxMessageId = null)
            {
                var handlerType = typeof(IMessageHandler<>).MakeGenericType(message.GetType());
                var idAwareHandlerType = typeof(IIdAwareMessageHandler<>).MakeGenericType(message.GetType());

                using var probe = _scopeFactory.CreateScope();
                var handlers = probe.ServiceProvider.GetServices(handlerType).ToArray();
                foreach (var handler in handlers)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var scopedHandler = scope.ServiceProvider.GetServices(handlerType)
                        .Single(candidate => candidate.GetType() == handler.GetType());
                    var isIdAware = outboxMessageId.HasValue && idAwareHandlerType.IsInstanceOfType(scopedHandler);
                    var method = isIdAware
                        ? idAwareHandlerType.GetMethod(nameof(IIdAwareMessageHandler<IMessage>.HandleAsync))
                        : handlerType.GetMethod(nameof(IMessageHandler<IMessage>.HandleAsync));
                    if (method is null)
                        continue;

                    var arguments = isIdAware
                        ? new object[] { message, outboxMessageId!.Value, CancellationToken.None }
                        : new object[] { message, CancellationToken.None };
                    await (Task)method.Invoke(scopedHandler, arguments)!;
                }
            }

            public Task<TResult> SendAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default)
                => throw new NotSupportedException("Queries are not used by the SQL Server E2E module client.");
        }
    }
}
