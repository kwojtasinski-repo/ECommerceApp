using ECommerceApp.Application.Identity.IAM.Services;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Domain.Identity.IAM;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Identity.IAM.Adapters;
using ECommerceApp.Infrastructure.Identity.IAM.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ECommerceApp.Infrastructure.Identity.IAM.Auth
{
    internal static class Extensions
    {
        public static IServiceCollection AddIamInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<IamDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<IamDbContext>();

            services.AddScoped<IDbContextMigrator, DbContextMigrator<IamDbContext>>();

            return services
                .AddScoped(typeof(ISignInManager<>), typeof(SignInManagerInternal<>))
                .AddScoped(typeof(IUserManager<>), typeof(UserManagerInternal<>))
                .AddScoped(typeof(IUserStore<>), typeof(UserStore<>))
                .Configure<AuthOptions>(configuration.GetSection("Jwt"))
                .AddSingleton<IJwtManager, JwtManager>()
                .AddSingleton<IRefreshTokenOptions>(sp =>
                    sp.GetRequiredService<IOptions<AuthOptions>>().Value)
                .AddScoped<IRefreshTokenRepository, RefreshTokenRepository>()
                .AddScoped<IUserContext, UserContext>()
                .AddScoped<IUserEmailResolver, UserEmailResolverAdapter>();
        }
    }
}
