using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Web.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using tusdotnet;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Stores;

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

    /// <summary>
    /// Registers the TUS disk store as a singleton ITusStore.
    /// The store path defaults to a system temp directory; override via
    /// appsettings.json: <c>Catalog:TusStorePath</c>.
    /// Always registered so ImageController can always inject ITusStore;
    /// the actual middleware is added conditionally via <see cref="UseTusUpload"/>.
    /// </summary>
    public static IServiceCollection AddTusServices(this IServiceCollection services, IConfiguration configuration)
    {
        var storePath = configuration.GetValue<string>("Catalog:TusStorePath")
                        ?? Path.Combine(Path.GetTempPath(), "tus-uploads-ecommerce");
        Directory.CreateDirectory(storePath);
        services.AddSingleton<ITusStore>(new TusDiskStore(storePath));
        return services;
    }

    /// <summary>
    /// Adds the tusdotnet middleware at <c>/tus</c>.
    /// Only add this to the pipeline when <c>CatalogOptions.UseTusUpload</c> is true.
    /// Auth is enforced inside the middleware via <c>OnAuthorizeAsync</c> because tusdotnet
    /// runs outside the MVC routing pipeline and does not respect <c>[Authorize]</c>.
    /// </summary>
    public static IApplicationBuilder UseTusUpload(this IApplicationBuilder app)
    {
        var store = app.ApplicationServices.GetRequiredService<ITusStore>();

        return app.UseTus(httpContext => Task.FromResult<DefaultTusConfiguration?>(new DefaultTusConfiguration
        {
            Store = store,
            UrlPath = "/tus",
            Events = new Events
            {
                OnAuthorizeAsync = ctx =>
                {
                    if (ctx.HttpContext.User.Identity?.IsAuthenticated != true)
                        ctx.FailRequest(HttpStatusCode.Unauthorized);
                    return Task.CompletedTask;
                }
            }
        }));
    }
}
