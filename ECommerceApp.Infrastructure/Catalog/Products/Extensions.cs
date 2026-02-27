using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Infrastructure.Catalog.Products.Repositories;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.Catalog.Products
{
    internal static class Extensions
    {
        public static IServiceCollection AddCatalogInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<CatalogDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IDbContextMigrator, DbContextMigrator<CatalogDbContext>>();

            return services
                .AddScoped<IProductRepository, ProductRepository>()
                .AddScoped<ICategoryRepository, CategoryRepository>()
                .AddScoped<IProductTagRepository, ProductTagRepository>();
        }
    }
}
