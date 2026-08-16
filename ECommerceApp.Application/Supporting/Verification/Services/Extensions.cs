using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Supporting.Verification.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddVerificationServices(this IServiceCollection services)
        {
            return services.AddScoped<IVerificationCodeService, VerificationCodeService>();
        }
    }
}