using ECommerceApp.Application.Services.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            return services;
        }
    }
}
