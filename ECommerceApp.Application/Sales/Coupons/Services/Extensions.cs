using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Coupons.Handlers;
using ECommerceApp.Application.Sales.Coupons.Rules;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ECommerceApp.Application.Sales.Coupons.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddCouponServices(this IServiceCollection services)
        {
            return services
                .AddCouponRuleEngine()
                // Evaluators and CouponService take CouponsOptions directly rather than IOptions<>,
                // so resolve the bound value once. Infrastructure binds the appsettings section over
                // the class defaults registered in AddApplication.
                .AddSingleton(sp => sp.GetRequiredService<IOptions<CouponsOptions>>().Value)
                .AddScoped<ICouponService, CouponService>()
                .AddScoped<IMessageHandler<OrderCancelled>, CouponsOrderCancelledHandler>()
                .AddScoped<IMessageHandler<PaymentExpired>, CouponsPaymentExpiredHandler>()
                .AddScoped<IMessageHandler<ProductNameChanged>, ProductNameChangedHandler>()
                .AddScoped<IMessageHandler<CategoryNameChanged>, CategoryNameChangedHandler>()
                .AddScoped<IMessageHandler<TagNameChanged>, TagNameChangedHandler>();
        }
    }
}
