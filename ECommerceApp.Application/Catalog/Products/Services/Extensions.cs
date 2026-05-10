using ECommerceApp.Application.Catalog.Images.Services;
using ECommerceApp.Application.Catalog.Images.Upload;
using ECommerceApp.Application.Catalog.Products.Handlers;
using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.FileManager;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Catalog.Products.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddCatalogServices(this IServiceCollection services)
        {
            services.AddMemoryCache();
            return services
                .AddScoped<IProductService, ProductService>()
                .AddScoped<ICategoryService, CategoryService>()
                .AddScoped<ICatalogNavigationService, CachedCatalogNavigationService>()
                .AddScoped<IProductTagService, ProductTagService>()
                .AddSingleton<IImageUrlBuilder, RelativeImageUrlBuilder>()
                .AddTransient<IImageService, ImageService>()
                .AddTransient<IUrlImageResolver, UrlImageResolver>()
                .AddSingleton<UploadSessionStore>()
                .AddTransient<IChunkedUploadService, ChunkedUploadService>()
                // Cache invalidation — one registration per message type so ModuleClient finds them
                .AddScoped<IMessageHandler<ProductUpdated>, ProductCacheInvalidationHandler>()
                .AddScoped<IMessageHandler<ProductPublished>, ProductCacheInvalidationHandler>()
                .AddScoped<IMessageHandler<ProductUnpublished>, ProductCacheInvalidationHandler>()
                .AddScoped<IMessageHandler<ProductDiscontinued>, ProductCacheInvalidationHandler>();
        }
    }
}
