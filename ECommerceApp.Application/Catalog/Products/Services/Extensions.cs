using ECommerceApp.Application.Catalog.Images.Services;
using ECommerceApp.Application.Catalog.Images.Upload;
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
                .AddScoped<ICatalogNavigationService, CachedCatalogNavigationService>()
                .AddScoped<IProductTagService, ProductTagService>()
                .AddSingleton<IImageUrlBuilder, RelativeImageUrlBuilder>()
                .AddTransient<IImageService, ImageService>()
                .AddTransient<IUrlImageResolver, UrlImageResolver>()
                .AddSingleton<UploadSessionStore>()
                .AddTransient<IChunkedUploadService, ChunkedUploadService>();
        }
    }
}
