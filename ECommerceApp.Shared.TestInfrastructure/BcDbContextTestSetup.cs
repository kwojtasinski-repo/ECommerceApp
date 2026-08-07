using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace ECommerceApp.Shared.TestInfrastructure
{
    public static class BcDbContextTestSetup
    {
        public static void ReplaceAllBcDbContextsWithInMemory(IServiceCollection services)
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

        public static void MakeAllBcDbContextsTransient(IServiceCollection services)
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

        public static void ReplaceDbContextMigratorsWithNoOp(IServiceCollection services)
        {
            var migrators = services
                .Where(d => d.ServiceType == typeof(IDbContextMigrator))
                .ToList();

            foreach (var descriptor in migrators)
            {
                services.Remove(descriptor);
            }

            services.AddScoped<IDbContextMigrator, NoOpDbContextMigrator>();
        }

        public static void EnsureAllBcDbContextsCreated(IServiceCollection services)
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