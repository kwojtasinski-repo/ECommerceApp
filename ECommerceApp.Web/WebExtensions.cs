using System;
using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Web.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Web;

internal static class WebExtensions
{
    private const int DefaultStorefrontIndexTtlSeconds = 60;

    /// <summary>
    /// Registers OutputCache policy and Web-layer cache invalidation handlers.
    /// IOutputCacheStore is an ASP.NET Core infrastructure type — it lives here, not in Application.
    /// TTL is read from appsettings.json: Cache:StorefrontIndexTtlSeconds (default: 60).
    /// </summary>
    public static IServiceCollection AddWebCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var ttlSeconds = configuration.GetValue("Cache:StorefrontIndexTtlSeconds", DefaultStorefrontIndexTtlSeconds);

        services.AddOutputCache(options =>
        {
            options.AddPolicy("StorefrontIndex", policy =>
                policy.Cache()
                      .SetVaryByQuery("searchString", "categoryId", "pageNo", "pageSize")
                      .Tag(StorefrontOutputCacheHandler.StorefrontIndexTag)
                      .Expire(TimeSpan.FromSeconds(ttlSeconds))
                      .With(ctx => !ctx.HttpContext.User.Identity!.IsAuthenticated));
        });

        // One registration per message type so ModuleClient finds them all.
        services.AddScoped<IMessageHandler<ProductUpdated>, StorefrontOutputCacheHandler>();
        services.AddScoped<IMessageHandler<ProductPublished>, StorefrontOutputCacheHandler>();
        services.AddScoped<IMessageHandler<ProductUnpublished>, StorefrontOutputCacheHandler>();
        services.AddScoped<IMessageHandler<ProductDiscontinued>, StorefrontOutputCacheHandler>();

        return services;
    }
}
