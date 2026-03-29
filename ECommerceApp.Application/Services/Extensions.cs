using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            return services;
        }
    }
}
