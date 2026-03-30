using ECommerceApp.Application.Supporting.TimeManagement;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Identity.IAM.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddIamServices(this IServiceCollection services)
        {
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddTransient<IUserManagementService, UserManagementService>();
            services.AddScoped<IScheduledTask, RefreshTokenCleanupTask>();
            return services;
        }
    }
}
