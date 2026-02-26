using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Messaging
{
    internal static class Extensions
    {
        public static IServiceCollection AddMessagingServices(this IServiceCollection services)
        {
            return services;
        }
    }
}
