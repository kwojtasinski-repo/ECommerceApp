using ECommerceApp.Application.Services.Addresses;
using ECommerceApp.Application.Services.Authentication;
using ECommerceApp.Application.Services.Brands;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddTransient<IAddressService, AddressService>();
            services.AddTransient<IBrandService, BrandService>();
            services.AddHttpContextAccessor();
            return services;
        }
    }
}
