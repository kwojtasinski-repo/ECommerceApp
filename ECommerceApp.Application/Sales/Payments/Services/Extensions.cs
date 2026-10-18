using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments.Handlers;
using ECommerceApp.Application.Supporting.TimeManagement;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Sales.Payments.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddPaymentServices(this IServiceCollection services)
        {
            return services
                .AddScoped<IPaymentService, PaymentService>()
                .AddScoped<IScheduledTask, PaymentWindowExpiredJob>()
                .AddScoped<IMessageHandler<OrderPlaced>, OrderPlacedHandler>()
                .AddScoped<IMessageHandler<RefundApproved>, PaymentRefundApprovedHandler>();
        }
    }
}
