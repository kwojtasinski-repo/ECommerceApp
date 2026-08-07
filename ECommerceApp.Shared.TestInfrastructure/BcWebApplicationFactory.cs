using ECommerceApp.Application.Messaging;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Identity.IAM;
using ECommerceApp.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Shared.TestInfrastructure
{
    /// <summary>
    /// <para>
    /// WebApplicationFactory for new BC integration tests. Differences from the legacy
    /// <see cref="CustomWebApplicationFactory{TStartup}"/>:
    /// </para>
    /// <list type="number">
    ///   <item>Replaces ALL per-BC DbContexts (including <c>internal sealed</c> ones)
    ///         with InMemory databases — no SQL Server dependency.</item>
    ///   <item>Replaces <see cref="IMessageBroker"/> with <see cref="SynchronousMultiHandlerBroker"/>
    ///         — dispatches to ALL registered handlers synchronously.</item>
    ///   <item>Replaces <see cref="IDbContextMigrator"/> registrations with no-op stubs
    ///         (InMemory databases don't support migrations).</item>
    /// </list>
    /// </summary>
    public class BcWebApplicationFactory : CustomWebApplicationFactory<Startup>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Let the base class handle the legacy Context → InMemory swap + seed data
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                BcDbContextTestSetup.ReplaceAllBcDbContextsWithInMemory(services);
                BcDbContextTestSetup.MakeAllBcDbContextsTransient(services);
                ReplaceMessageBrokerWithSynchronous(services);
                BcDbContextTestSetup.ReplaceDbContextMigratorsWithNoOp(services);
                EnsureIamDbContextCreatedAndSeeded(services);
                BcDbContextTestSetup.EnsureAllBcDbContextsCreated(services);
            });
        }

        /// <summary>
        /// Replaces <see cref="IMessageBroker"/> with <see cref="SynchronousMultiHandlerBroker"/>.
        /// Also replaces <see cref="IModuleClient"/> registration to avoid dangling references.
        /// </summary>
        private static void ReplaceMessageBrokerWithSynchronous(IServiceCollection services)
        {
            // Remove existing IMessageBroker registration
            var brokerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMessageBroker));
            if (brokerDescriptor != null)
                services.Remove(brokerDescriptor);

            services.AddScoped<IMessageBroker, SynchronousMultiHandlerBroker>();
        }

        /// <summary>
        /// Ensures all per-BC InMemory databases have their schemas created.
        /// Call from test setup or override <see cref="OverrideServicesImplementation"/>.
        /// </summary>
        public void EnsureAllDbContextsCreated()
        {
            using var scope = Services.CreateScope();
            var sp = scope.ServiceProvider;

            // Find all registered DbContext types and call EnsureCreated
            var contextTypes = new List<Type>();
            foreach (var service in sp.GetServices<DbContext>())
            {
                service.Database.EnsureCreated();
            }

            // For DbContexts that aren't registered as DbContext directly,
            // we rely on the fact that EF Core calls OnModelCreating on first use
        }

        private static void EnsureIamDbContextCreatedAndSeeded(IServiceCollection services)
        {
            using var tempSp = services.BuildServiceProvider();
            using var scope = tempSp.CreateScope();
            var iamContext = scope.ServiceProvider.GetRequiredService<IamDbContext>();
            iamContext.Database.EnsureCreated();

            try
            {
                Utilities.InitializeIamUsers(scope.ServiceProvider).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // IamDbContext is swapped to one fixed InMemory database name shared by every
                // BcWebApplicationFactory/CustomWebApplicationFactory instance (not a per-instance GUID
                // like the BC-specific DbContexts get) — with xunit.runner.json's
                // parallelizeTestCollections now true, two test classes' constructors can race to seed
                // the same fixed-Id test users concurrently. Whichever wins leaves the data every
                // instance reads from the same named store anyway; the loser here just needs to not
                // crash its own host startup over it, matching CustomWebApplicationFactory's own
                // try/catch around this same seeding call.
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<BcWebApplicationFactory>>();
                logger.LogWarning(ex, "IAM user seeding raced with another test host — continuing.");
            }
        }
    }
}

