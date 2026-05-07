using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.External;
using ECommerceApp.Application.FileManager;
using ECommerceApp.Application.Identity.IAM.Services;
using ECommerceApp.Application.Middlewares;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Supporting.Currencies.Services;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Sales.Coupons.Services;
using ECommerceApp.Application.Sales.Fulfillment.Services;
using ECommerceApp.Application.Sales.Payments.Services;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Application.Supporting.Communication;
using ECommerceApp.Application.Supporting.TimeManagement.Services;
using ECommerceApp.Application.Backoffice;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
namespace ECommerceApp.Application
{
    public static class DependencyInjection 
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddFilesStore();
            services.AddErrorHandling();
            services.AddNbpClient();
            services.AddServices();
            services.AddIamServices();
            services.AddUserProfileServices();
            services.AddCatalogServices();
            services.AddCurrencyServices();
            services.AddTimeManagementServices();
            services.AddMessagingServices();
            services.AddAvailabilityServices();
            services.AddPresaleServices();
            services.AddOrderServices();
            services.AddPaymentServices();
            services.AddCouponServices();
            services.AddFulfillmentServices();
            services.AddCommunicationServices();
            services.AddBackoffice();
            services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
            return services;
        }
    }
}
