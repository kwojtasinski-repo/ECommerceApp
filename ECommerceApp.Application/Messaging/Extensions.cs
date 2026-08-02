using Microsoft.Extensions.DependencyInjection;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Messaging.Services;

namespace ECommerceApp.Application.Messaging
{
    internal static class Extensions
    {
        public static IServiceCollection AddMessagingServices(this IServiceCollection services)
        {
            services.AddScoped<IScheduledTask, OutboxCleanupTask>();
            services.AddScoped<IScheduledTask, InboxCleanupTask>();
            return services;
        }
    }
}
