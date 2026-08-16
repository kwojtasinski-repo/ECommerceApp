using ECommerceApp.Domain.Supporting.Verification;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Supporting.Verification.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.Supporting.Verification
{
    internal static class Extensions
    {
        public static IServiceCollection AddVerificationInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<VerificationDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IVerificationDbContext>(sp => sp.GetRequiredService<VerificationDbContext>())
                .AddScoped<IVerificationCodeRepository, VerificationCodeRepository>();

            services.AddScoped<IDbContextMigrator, DbContextMigrator<VerificationDbContext>>();

            return services;
        }
    }
}