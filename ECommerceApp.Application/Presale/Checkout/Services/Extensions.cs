using ECommerceApp.Application.Inventory.Availability.Messages;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Presale.Checkout.Handlers;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Supporting.TimeManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddPresaleServices(this IServiceCollection services)
        {
            services.AddMemoryCache();
            services.Configure<PresaleOptions>(_ => { });
            services.AddSingleton<IValidateOptions<PresaleOptions>, PresaleOptionsValidator>();
            return services
                .AddScoped<IStorefrontQueryService, StorefrontQueryService>()
                .AddScoped<ISoftReservationService, SoftReservationService>()
                .AddScoped<ICartService, CartService>()
                .AddScoped<ICheckoutService, CheckoutService>()
                .AddScoped<IScheduledTask, SoftReservationExpiredJob>()
                .AddScoped<IMessageHandler<StockAvailabilityChanged>, StockAvailabilityChangedHandler>()
                .AddScoped<IMessageHandler<OrderPlaced>, OrderPlacedHandler>()
                .AddScoped<IMessageHandler<OrderPlacementFailed>, OrderPlacementFailedHandler>();
        }
    }
}
