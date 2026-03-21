using ECommerceApp.Application.Messaging;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.IntegrationTests.Common
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
    ///   <item>Removes <c>BackgroundMessageDispatcher</c> hosted service to avoid
    ///         background dispatch competing with the synchronous broker.</item>
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
                ReplaceAllBcDbContextsWithInMemory(services);
                ReplaceMessageBrokerWithSynchronous(services);
                RemoveBackgroundMessageDispatcher(services);
                ReplaceDbContextMigratorsWithNoOp(services);
            });
        }

        /// <summary>
        /// Finds all <see cref="DbContextOptions{T}"/> registrations in the service collection
        /// (for any T that is a DbContext but is NOT the legacy <see cref="Context"/>)
        /// and replaces them with InMemory options.
        /// <para>
        /// This works even for <c>internal sealed</c> DbContext types because we never
        /// reference them by name — we match on <c>DbContextOptions&lt;&gt;</c> generic type
        /// at runtime.
        /// </para>
        /// </summary>
        private static void ReplaceAllBcDbContextsWithInMemory(IServiceCollection services)
        {
            var optionsDescriptors = services
                .Where(d => d.ServiceType.IsGenericType
                    && d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)
                    && d.ServiceType != typeof(DbContextOptions<Context>))
                .ToList();

            foreach (var descriptor in optionsDescriptors)
            {
                var dbContextType = descriptor.ServiceType.GetGenericArguments()[0];
                var dbName = $"BcTestDb_{dbContextType.Name}_{Guid.NewGuid():N}";

                services.Remove(descriptor);

                // Build InMemory options for the specific DbContext type
                var optionsBuilderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(dbContextType);
                var optionsBuilder = (DbContextOptionsBuilder)Activator.CreateInstance(optionsBuilderType)!;
                optionsBuilder.UseInMemoryDatabase(dbName);

                services.AddSingleton(descriptor.ServiceType, optionsBuilder.Options);
            }

            // Also remove any non-generic DbContextOptions that might conflict
            var nonGenericOptions = services
                .Where(d => d.ServiceType == typeof(DbContextOptions)
                    && d.ImplementationType != null
                    && d.ImplementationType != typeof(DbContextOptions<Context>))
                .ToList();

            foreach (var d in nonGenericOptions)
            {
                services.Remove(d);
            }
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
        /// Removes the <c>BackgroundMessageDispatcher</c> hosted service so it doesn't
        /// race with the synchronous broker reading from the same channel.
        /// </summary>
        private static void RemoveBackgroundMessageDispatcher(IServiceCollection services)
        {
            var backgroundDispatchers = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                    && d.ImplementationType?.Name == "BackgroundMessageDispatcher")
                .ToList();

            foreach (var d in backgroundDispatchers)
            {
                services.Remove(d);
            }
        }

        /// <summary>
        /// Replaces all <see cref="IDbContextMigrator"/> registrations with no-ops.
        /// InMemory databases don't support <c>MigrateAsync</c>.
        /// </summary>
        private static void ReplaceDbContextMigratorsWithNoOp(IServiceCollection services)
        {
            var migrators = services
                .Where(d => d.ServiceType == typeof(IDbContextMigrator))
                .ToList();

            foreach (var d in migrators)
            {
                services.Remove(d);
            }

            services.AddScoped<IDbContextMigrator, NoOpDbContextMigrator>();
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
    }
}
