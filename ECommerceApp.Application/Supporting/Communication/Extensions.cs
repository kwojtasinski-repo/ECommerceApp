using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Emails;
using ECommerceApp.Application.Supporting.Communication.Handlers;
using ECommerceApp.Application.Supporting.Communication.Services;
using Microsoft.Extensions.DependencyInjection;

using FulfillmentMessages = ECommerceApp.Application.Sales.Fulfillment.Messages;

namespace ECommerceApp.Application.Supporting.Communication
{
    internal static class Extensions
    {
        public static IServiceCollection AddCommunicationServices(this IServiceCollection services)
        {
            services.AddScoped<INotificationService, LoggingNotificationService>();
            services.AddScoped<IEmailService, LoggingEmailService>();
            services.AddScoped<IOrderUserResolver, NullOrderUserResolver>();
            services.AddScoped<IUserEmailResolver, NullUserEmailResolver>();
            services.AddScoped<IMessageHandler<OrderPlaced>, OrderPlacedNotificationHandler>();
            services.AddScoped<IMessageHandler<OrderCancelled>, OrderCancelledNotificationHandler>();
            services.AddScoped<IMessageHandler<OrderRequiresAttention>, OrderRequiresAttentionNotificationHandler>();
            services.AddScoped<IMessageHandler<PaymentConfirmed>, PaymentConfirmedNotificationHandler>();
            services.AddScoped<IMessageHandler<PaymentExpired>, PaymentExpiredNotificationHandler>();
            services.AddScoped<IMessageHandler<FulfillmentMessages.RefundApproved>, RefundApprovedNotificationHandler>();
            services.AddScoped<IMessageHandler<FulfillmentMessages.RefundRejected>, RefundRejectedNotificationHandler>();
            services.AddScoped<IMessageHandler<OrderPlaced>, OrderPlacedEmailHandler>();
            services.AddScoped<IMessageHandler<OrderCancelled>, OrderCancelledEmailHandler>();
            services.AddScoped<IMessageHandler<PaymentConfirmed>, PaymentConfirmedEmailHandler>();
            services.AddScoped<IMessageHandler<PaymentExpired>, PaymentExpiredEmailHandler>();
            services.AddScoped<IMessageHandler<FulfillmentMessages.RefundApproved>, RefundApprovedEmailHandler>();
            services.AddScoped<IMessageHandler<FulfillmentMessages.RefundRejected>, RefundRejectedEmailHandler>();
            return services;
        }
    }
}
