using ECommerceApp.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.Auth
{
    internal static class Extensions
    {
        public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration configuration)
        {
            return services
                .AddScoped(typeof(ISignInManager<>), typeof(SignInManagerInternal<>))
                .AddScoped(typeof(IUserManager<>), typeof(UserManagerInternal<>))
                .Configure<AuthOptions>(configuration.GetSection("Jwt"))
                .AddSingleton<IJwtManager, JwtManager>()
                .AddScoped<IUserContext, UserContext>();
        }
    }
}
