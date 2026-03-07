using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddPresaleServices(this IServiceCollection services)
        {
            services.Configure<PresaleOptions>(_ => { });
            services.AddSingleton<IValidateOptions<PresaleOptions>, PresaleOptionsValidator>();
            return services
                .AddScoped<ISoftReservationService, SoftReservationService>()
                .AddScoped<ICartService, CartService>();
        }
    }
}
