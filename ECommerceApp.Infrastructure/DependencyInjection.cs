using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Identity.IAM.Auth;
using ECommerceApp.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDatabase(configuration);
            services.AddRepositories();
            services.AddIamInfrastructure(configuration);
            return services;
        }
    }
}
