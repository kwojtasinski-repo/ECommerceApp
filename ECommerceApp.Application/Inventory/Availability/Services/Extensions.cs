using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.Inventory.Availability.Handlers;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Supporting.TimeManagement;
using Microsoft.Extensions.DependencyInjection;

using PaymentsRefundApproved = ECommerceApp.Application.Sales.Payments.Messages.RefundApproved;
using PaymentConfirmed = ECommerceApp.Application.Sales.Payments.Messages.PaymentConfirmed;

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
                // OrderShippedHandler unregistered — replaced by Fulfillment handlers (ADR-0017 §13.3)
                .AddScoped<IMessageHandler<PaymentConfirmed>, PaymentConfirmedHandler>()
                .AddScoped<IMessageHandler<PaymentsRefundApproved>, RefundApprovedHandler>()
                .AddScoped<IMessageHandler<ProductPublished>, ProductPublishedHandler>()
                .AddScoped<IMessageHandler<ProductUnpublished>, ProductUnpublishedHandler>()
                .AddScoped<IMessageHandler<ProductDiscontinued>, ProductDiscontinuedHandler>()
                .AddScoped<IMessageHandler<ShipmentDelivered>, ShipmentDeliveredHandler>()
                .AddScoped<IMessageHandler<ShipmentFailed>, ShipmentFailedHandler>()
                .AddScoped<IMessageHandler<ShipmentPartiallyDelivered>, ShipmentPartiallyDeliveredHandler>();
            return services;
        }
    }
}
