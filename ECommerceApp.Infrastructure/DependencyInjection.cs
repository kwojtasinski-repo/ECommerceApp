using ECommerceApp.Infrastructure.AccountProfile;
using ECommerceApp.Infrastructure.Catalog.Products;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Identity.IAM.Auth;
using ECommerceApp.Infrastructure.Repositories;
using ECommerceApp.Infrastructure.Messaging;
using ECommerceApp.Infrastructure.Supporting.Currencies;
using ECommerceApp.Infrastructure.Inventory.Availability;
using ECommerceApp.Infrastructure.Presale.Checkout;
using ECommerceApp.Infrastructure.Sales.Coupons;
using ECommerceApp.Infrastructure.Sales.Fulfillment;
using ECommerceApp.Infrastructure.Sales.Payments;
using ECommerceApp.Infrastructure.Sales.Orders;
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
            services.AddTimeManagementInfrastructure(configuration);
            services.AddAvailabilityInfrastructure(configuration);
            services.AddPresaleInfrastructure(configuration);
            services.AddOrdersInfrastructure(configuration);
            services.AddPaymentsInfrastructure(configuration);
            services.AddCouponsInfrastructure(configuration);
            services.AddFulfillmentInfrastructure(configuration);
            return services;
        }
    }
}
