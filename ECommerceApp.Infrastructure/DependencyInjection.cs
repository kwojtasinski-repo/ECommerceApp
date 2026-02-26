using ECommerceApp.Infrastructure.AccountProfile;
using ECommerceApp.Infrastructure.Catalog.Products;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Identity.IAM.Auth;
using ECommerceApp.Infrastructure.Repositories;
using ECommerceApp.Infrastructure.Messaging;
using ECommerceApp.Infrastructure.Supporting.Currencies;
using ECommerceApp.Infrastructure.Supporting.TimeManagement;
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
            services.AddUserProfileInfrastructure(configuration);
            services.AddCatalogInfrastructure(configuration);
            services.AddCurrencyInfrastructure(configuration);
            services.AddMessagingInfrastructure(configuration);
            services.AddTimeManagementInfrastructure(configuration, jobs =>
            {
                jobs.AddRecurring("CurrencyRateSync", cron: "15 12 * * *", maxRetries: 3);
                jobs.AddRecurring("PaymentExpiration", cron: "*/5 * * * *", maxRetries: 3);
                jobs.AddDeferred("PaymentTimeout", maxRetries: 2);
            });
            return services;
        }
    }
}
