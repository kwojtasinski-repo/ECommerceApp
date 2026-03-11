using ECommerceApp.Domain.Sales.Payments;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Sales.Payments.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.Sales.Payments
{
    internal static class Extensions
    {
        public static IServiceCollection AddPaymentsInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<PaymentsDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IDbContextMigrator, DbContextMigrator<PaymentsDbContext>>();
            services.AddScoped<IPaymentRepository, PaymentRepository>();

            return services;
        }
    }
}
