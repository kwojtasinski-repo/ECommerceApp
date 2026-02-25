using ECommerceApp.Domain.Supporting.Currencies;
using ECommerceApp.Infrastructure.Supporting.Currencies.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.Supporting.Currencies
{
    internal static class Extensions
    {
        public static IServiceCollection AddCurrencyInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<CurrencyDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            return services
                .AddScoped<ICurrencyRepository, CurrencyRepository>()
                .AddScoped<ICurrencyRateRepository, CurrencyRateRepository>();
        }
    }
}
