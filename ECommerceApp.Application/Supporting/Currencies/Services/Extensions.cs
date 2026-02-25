using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Supporting.Currencies.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddCurrencyServices(this IServiceCollection services)
        {
            return services
                .AddScoped<ICurrencyService, CurrencyService>()
                .AddScoped<ICurrencyRateService, CurrencyRateService>();
        }
    }
}
