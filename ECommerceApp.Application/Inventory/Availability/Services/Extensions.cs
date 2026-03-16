using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.Inventory.Availability.Handlers;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Application.Supporting.TimeManagement;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Inventory.Availability.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddAvailabilityServices(this IServiceCollection services)
        {
            services.AddMemoryCache();
            services
                .AddScoped<IStockService, StockService>()
                .AddScoped<IStockQueryService, StockQueryService>();
            services
                .AddScoped<IScheduledTask, PaymentWindowTimeoutJob>()
                .AddScoped<IScheduledTask, StockAdjustmentJob>();
            services
                .AddScoped<IMessageHandler<OrderPlaced>, OrderPlacedHandler>()
                .AddScoped<IMessageHandler<OrderCancelled>, OrderCancelledHandler>()
                .AddScoped<IMessageHandler<OrderShipped>, OrderShippedHandler>()
                .AddScoped<IMessageHandler<PaymentConfirmed>, PaymentConfirmedHandler>()
                .AddScoped<IMessageHandler<RefundApproved>, RefundApprovedHandler>()
                .AddScoped<IMessageHandler<ProductPublished>, ProductPublishedHandler>()
                .AddScoped<IMessageHandler<ProductUnpublished>, ProductUnpublishedHandler>()
                .AddScoped<IMessageHandler<ProductDiscontinued>, ProductDiscontinuedHandler>();
            return services;
        }
    }
}
