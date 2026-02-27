using ECommerceApp.Domain.AccountProfile;
using ECommerceApp.Infrastructure.AccountProfile.Repositories;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.AccountProfile
{
    internal static class Extensions
    {
        public static IServiceCollection AddUserProfileInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<UserProfileDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IDbContextMigrator, DbContextMigrator<UserProfileDbContext>>();

            return services
                .AddScoped<IUserProfileRepository, UserProfileRepository>();
        }
    }
}
