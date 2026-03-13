using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Sales.Fulfillment.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddFulfillmentServices(this IServiceCollection services)
        {
            return services
                .AddScoped<IRefundService, RefundService>();
        }
    }
}
