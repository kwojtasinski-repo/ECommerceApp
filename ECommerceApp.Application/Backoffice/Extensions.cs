using ECommerceApp.Application.Backoffice.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Backoffice
{
    internal static class Extensions
    {
        internal static IServiceCollection AddBackoffice(this IServiceCollection services)
        {
            services.AddScoped<IBackofficeOrderService, BackofficeOrderService>();
            services.AddScoped<IBackofficePaymentService, BackofficePaymentService>();
            services.AddScoped<IBackofficeCatalogService, BackofficeCatalogService>();
            services.AddScoped<IBackofficeCustomerService, BackofficeCustomerService>();
            services.AddScoped<IBackofficeUserService, BackofficeUserService>();
            services.AddScoped<IBackofficeCouponService, BackofficeCouponService>();
            services.AddScoped<IBackofficeCurrencyService, BackofficeCurrencyService>();
            services.AddScoped<IBackofficeJobService, BackofficeJobService>();
            services.AddScoped<IBackofficeRefundService, BackofficeRefundService>();
            return services;
        }
    }
}
