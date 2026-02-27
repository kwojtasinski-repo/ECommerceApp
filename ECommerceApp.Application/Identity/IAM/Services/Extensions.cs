using ECommerceApp.Domain.Identity.IAM;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Identity.IAM.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddIamServices(this IServiceCollection services)
        {
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddTransient<IUserManagementService, UserManagementService>();
            return services;
        }
    }
}
