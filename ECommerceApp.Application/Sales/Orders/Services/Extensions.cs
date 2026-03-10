using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Handlers;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Supporting.TimeManagement;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Sales.Orders.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddOrderServices(this IServiceCollection services)
        {
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<IOrderItemService, OrderItemService>();
            services.AddScoped<IScheduledTask, SnapshotOrderItemsJob>();
            services.AddScoped<IMessageHandler<OrderPlaced>, OrderPlacedSnapshotHandler>();
            return services;
        }
    }
}
