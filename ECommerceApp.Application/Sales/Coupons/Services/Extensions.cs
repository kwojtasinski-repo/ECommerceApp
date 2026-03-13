using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Coupons.Contracts;
using ECommerceApp.Application.Sales.Coupons.Handlers;
using ECommerceApp.Application.Sales.Coupons.Services;
using ECommerceApp.Application.Sales.Orders.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Sales.Coupons.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddCouponServices(this IServiceCollection services)
        {
            return services
                .AddScoped<ICouponService, CouponService>()
                .AddScoped<IMessageHandler<OrderCancelled>, CouponsOrderCancelledHandler>();
        }
    }
}
