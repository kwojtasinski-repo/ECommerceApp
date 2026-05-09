using ECommerceApp.Application.Sales.Coupons.Contracts;
using ECommerceApp.Application.Sales.Shared.Contracts;
using ECommerceApp.Domain.Sales.Coupons;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Sales.Coupons.Adapters;
using ECommerceApp.Infrastructure.Sales.Coupons.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.Sales.Coupons
{
    internal static class Extensions
    {
        public static IServiceCollection AddCouponsInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddMemoryCache();
            services.AddDbContext<CouponsDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<ICouponsDbContext>(sp => sp.GetRequiredService<CouponsDbContext>());

            services.AddScoped<IDbContextMigrator, DbContextMigrator<CouponsDbContext>>();
            services.AddScoped<ICouponRepository, CouponRepository>();
            services.AddScoped<ICouponUsedRepository, CouponUsedRepository>();
            services.AddScoped<ICouponApplicationRecordRepository, CouponApplicationRecordRepository>();
            services.AddScoped<IOrderExistenceChecker, ECommerceApp.Infrastructure.Sales.Shared.Adapters.OrderExistenceCheckerAdapter>();
            services.AddScoped<IStockAvailabilityChecker, StockAvailabilityCheckerAdapter>();
            services.AddScoped<ICompletedOrderCounter, CompletedOrderCounterAdapter>();
            services.AddScoped<ISpecialEventCache, SpecialEventCache>();
            services.AddScoped<IRuntimeCouponSource, NullRuntimeCouponSource>();
            services.AddScoped<IScopeTargetRepository, ScopeTargetRepository>();

            return services;
        }
    }
}
