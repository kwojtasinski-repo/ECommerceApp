using ECommerceApp.Application.Interfaces;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.Identity.IAM.Auth
{
    internal static class Extensions
    {
        public static IServiceCollection AddIamInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var iamFeatureOptions = configuration.GetSection("Iam").Get<IamFeatureOptions>() ?? new IamFeatureOptions();

            services.AddDbContext<IamDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            var identityBuilder = services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
                    .AddRoles<IdentityRole>();

            if (iamFeatureOptions.UseIamStore)
            {
                identityBuilder.AddEntityFrameworkStores<IamDbContext>();
            }
            else
            {
                identityBuilder.AddEntityFrameworkStores<Context>();
            }

            services.AddScoped<IDbContextMigrator, DbContextMigrator<IamDbContext>>();

            return services
                .AddScoped(typeof(ISignInManager<>), typeof(SignInManagerInternal<>))
                .AddScoped(typeof(IUserManager<>), typeof(UserManagerInternal<>))
                .Configure<AuthOptions>(configuration.GetSection("Jwt"))
                .AddSingleton<IJwtManager, JwtManager>()
                .AddScoped<IUserContext, UserContext>();
        }
    }
}
