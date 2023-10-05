using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.Database
{
    internal static class Extensions
    {
        public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<Context>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection")));
            return services;
        }
    }
}
