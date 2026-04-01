using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.Repositories
{
    internal static class Extensions
    {
        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddTransient(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddTransient<IImageRepository, ImageRepository>();
            return services;
        }
    }
}
