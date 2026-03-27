using ECommerceApp.Application.Services.Brands;
using ECommerceApp.Application.Services.Items;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ECommerceApp.UnitTests")]
[assembly: InternalsVisibleTo("ECommerceApp.IntegrationTests")]
namespace ECommerceApp.Application.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.AddTransient<IBrandService, BrandService>();
            services.AddTransient<IImageService, ImageService>();
            services.AddHttpContextAccessor();
            return services;
        }
    }
}
