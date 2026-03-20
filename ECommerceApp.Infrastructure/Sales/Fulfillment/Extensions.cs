using ECommerceApp.Application.Sales.Fulfillment.Contracts;
using ECommerceApp.Domain.Sales.Fulfillment;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Sales.Fulfillment.Adapters;
using ECommerceApp.Infrastructure.Sales.Fulfillment.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.Sales.Fulfillment
{
    internal static class Extensions
    {
        public static IServiceCollection AddFulfillmentInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<FulfillmentDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IDbContextMigrator, DbContextMigrator<FulfillmentDbContext>>();
            services.AddScoped<IRefundRepository, RefundRepository>();
            services.AddScoped<IShipmentRepository, ShipmentRepository>();
            services.AddScoped<IOrderExistenceChecker, OrderExistenceCheckerAdapter>();

            return services;
        }
    }
}
