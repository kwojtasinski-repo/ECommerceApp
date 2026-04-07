using ECommerceApp.Application.Sales.Orders.Contracts;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Sales.Orders.Adapters;
using ECommerceApp.Infrastructure.Sales.Orders.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.Sales.Orders
{
    internal static class Extensions
    {
        public static IServiceCollection AddOrdersInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<OrdersDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IDbContextMigrator, DbContextMigrator<OrdersDbContext>>();

            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IOrderItemRepository, OrderItemRepository>();
            services.AddScoped<ICustomerExistenceChecker, CustomerExistenceChecker>();
            services.AddScoped<IOrderCustomerResolver, OrderCustomerResolver>();
            services.AddScoped<IOrderProductResolver, OrderProductResolver>();
            services.AddScoped<IOrderUserResolver, OrderUserResolverAdapter>();

            return services;
        }
    }
}
