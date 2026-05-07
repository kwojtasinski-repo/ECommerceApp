using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace ECommerceApp.Shared.TestInfrastructure
{
    /// <summary>
    /// Extension methods for replacing DbContext registrations with InMemory equivalents
    /// in <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/> tests.
    /// </summary>
    public static class DbContextTestExtensions
    {
        /// <summary>
        /// Fully replaces a known, named DbContext (e.g. legacy Context or IamDbContext)
        /// with an InMemory version. Removes the context registration itself, its options, and all
        /// EF Core configuration delegates, then re-registers via AddDbContext.
        /// </summary>
        public static IServiceCollection ReplaceDbContextWithInMemory<TContext>(
            this IServiceCollection services,
            string dbName)
            where TContext : DbContext
        {
            services.RemoveDbContextOptionsRegistrations<TContext>();
            var contextDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TContext));
            if (contextDescriptor != null) 
            {
                services.Remove(contextDescriptor); 
            }

            services.AddDbContext<TContext>(options => options.UseInMemoryDatabase(dbName));
            return services;
        }

        /// <summary>
        /// Replaces the options for a BC DbContext discovered at runtime by type.
        /// Only the options and EF Core configuration delegates are replaced; the existing
        /// scoped context registration is preserved so the DI graph still resolves.
        /// </summary>
        public static IServiceCollection ReplaceDbContextWithInMemory(
            this IServiceCollection services,
            Type dbContextType,
            string dbName)
        {
            services.RemoveDbContextOptionsRegistrations(dbContextType);

            var optionsBuilderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(dbContextType);
            var optionsBuilder = (DbContextOptionsBuilder)Activator.CreateInstance(optionsBuilderType)!;
            optionsBuilder
                .UseInMemoryDatabase(dbName)
                .ReplaceService<IValueGeneratorSelector, TypedIdAwareValueGeneratorSelector>();

            var optionsServiceType = typeof(DbContextOptions<>).MakeGenericType(dbContextType);
            services.AddSingleton(optionsServiceType, optionsBuilder.Options);
            return services;
        }

        /// <summary>
        /// Removes every EF Core options-related registration for TContext:
        /// DbContextOptions&lt;TContext&gt; and any single-argument generic service whose
        /// sole type argument is TContext (covers IDbContextOptionsConfiguration&lt;T&gt;
        /// from EF Core 8+ without hard-coding the type name).
        /// The DbContext registration itself is NOT removed.
        /// </summary>
        public static IServiceCollection RemoveDbContextOptionsRegistrations<TContext>(
            this IServiceCollection services)
            where TContext : DbContext
            => services.RemoveDbContextOptionsRegistrations(typeof(TContext));

        /// <summary>Non-generic overload.</summary>
        public static IServiceCollection RemoveDbContextOptionsRegistrations(
            this IServiceCollection services,
            Type dbContextType)
        {
            var decsriptorsToRemove = services
                .Where(d => IsOptionsForContext(d.ServiceType, dbContextType))
                .ToList();

            foreach (var descriptor in decsriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            return services;
        }

        /// <summary>
        /// Returns true for DbContextOptions&lt;TContext&gt; and any closed generic with a
        /// single argument equal to dbContextType (catches EF Core 8+ internals without
        /// hard-coding type names). Intentionally excludes the DbContext type itself.
        /// </summary>
        private static bool IsOptionsForContext(Type serviceType, Type dbContextType)
        {
            if (serviceType == typeof(DbContextOptions<>).MakeGenericType(dbContextType))
            {
                return true;
            }

            if (!serviceType.IsGenericType)
            {
                return false;
            }

            var args = serviceType.GetGenericArguments();
            return args.Length == 1
                && args[0] == dbContextType
                && serviceType != dbContextType;
        }
    }
}


