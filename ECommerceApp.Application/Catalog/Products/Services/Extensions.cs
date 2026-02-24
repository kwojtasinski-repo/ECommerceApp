using ECommerceApp.Application.FileManager;
using ECommerceApp.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Catalog.Products.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddCatalogServices(this IServiceCollection services)
        {
            return services
                .AddScoped<IProductService, ProductService>()
                .AddScoped<ICategoryService, CategoryService>()
                .AddScoped<IProductTagService, ProductTagService>()
                .AddSingleton<IImageUrlBuilder, RelativeImageUrlBuilder>();
        }
    }
}
