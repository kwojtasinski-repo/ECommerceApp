using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Sales.Orders.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddOrderServices(this IServiceCollection services)
        {
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<IOrderItemService, OrderItemService>();
            return services;
        }
    }
}
