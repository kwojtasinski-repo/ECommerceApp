using ECommerceApp.Application.Messaging;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
                MakeAllBcDbContextsTransient(services);
                ReplaceMessageBrokerWithSynchronous(services);
                ReplaceDbContextMigratorsWithNoOp(services);
                EnsureAllBcDbContextsCreated(services);
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
            var bcContextTypes = services
                .Where(d => d.ServiceType.IsGenericType
                    && d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)
                    && d.ServiceType != typeof(DbContextOptions<Context>))
                .Select(d => d.ServiceType.GetGenericArguments()[0])
                .ToList();

            foreach (var dbContextType in bcContextTypes)
            {
                var dbName = $"BcTestDb_{dbContextType.Name}_{Guid.NewGuid():N}";
                services.ReplaceDbContextWithInMemory(dbContextType, dbName);
            }
        }

        /// <summary>
        /// Changes all per-BC DbContext registrations from <see cref="ServiceLifetime.Scoped"/>
        /// to <see cref="ServiceLifetime.Transient"/>.
        /// <para>
        /// In production, each HTTP request creates a new DI scope (and thus a new DbContext instance).
        /// In tests, we don't create new scopes between service calls, so the same scoped DbContext
        /// retains change-tracker state between operations. This causes tracking conflicts when
        /// <c>AsNoTracking()</c> queries return new instances that are then <c>Update()</c>-ed while
        /// the original entity from a previous <c>Add()</c> is still tracked.
        /// </para>
        /// <para>
        /// Converting all scoped registrations to transient means each service resolution gets a fresh
        /// instance tree (DbContext, repositories, services) with empty change trackers,
        /// while the InMemory database (shared by name) retains the data across instances.
        /// </para>
        /// </summary>
        private static void MakeAllBcDbContextsTransient(IServiceCollection services)
        {
            var scopedDescriptors = services
                .Where(d => d.Lifetime == ServiceLifetime.Scoped
                    && d.ServiceType != typeof(Context))
                .ToList();

            foreach (var descriptor in scopedDescriptors)
            {
                services.Remove(descriptor);

                if (descriptor.ImplementationFactory != null)
                {
                    services.Add(new ServiceDescriptor(
                        descriptor.ServiceType,
                        descriptor.ImplementationFactory,
                        ServiceLifetime.Transient));
                }
                else
                {
                    services.Add(new ServiceDescriptor(
                        descriptor.ServiceType,
                        descriptor.ImplementationType ?? descriptor.ServiceType,
                        ServiceLifetime.Transient));
                }
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

        /// <summary>
        /// Ensures all per-BC InMemory databases have their schemas created (applies HasData seeds).
        /// Called automatically during host setup after all InMemory replacements are complete.
        /// </summary>
        private static void EnsureAllBcDbContextsCreated(IServiceCollection services)
        {
            var bcContextTypes = services
                .Where(d => d.ServiceType.IsGenericType
                    && d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)
                    && d.ServiceType != typeof(DbContextOptions<Context>))
                .Select(d => d.ServiceType.GetGenericArguments()[0])
                .ToList();

            using var tempSp = services.BuildServiceProvider();
            using var scope = tempSp.CreateScope();
            foreach (var ctxType in bcContextTypes)
            {
                if (scope.ServiceProvider.GetService(ctxType) is DbContext ctx)
                {
                    ctx.Database.EnsureCreated();
                }
            }
        }
    }
}
