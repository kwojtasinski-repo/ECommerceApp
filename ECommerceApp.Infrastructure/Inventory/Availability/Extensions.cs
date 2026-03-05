using ECommerceApp.Application.Inventory.Availability;
using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Inventory.Availability.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.Inventory.Availability
{
    internal static class Extensions
    {
        public static IServiceCollection AddAvailabilityInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<InventoryOptions>(configuration.GetSection(InventoryOptions.SectionName));

            services.AddDbContext<AvailabilityDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IDbContextMigrator, DbContextMigrator<AvailabilityDbContext>>();

            return services
                .AddScoped<IStockItemRepository, StockItemRepository>()
                .AddScoped<IReservationRepository, ReservationRepository>()
                .AddScoped<IProductSnapshotRepository, ProductSnapshotRepository>()
                .AddScoped<IPendingStockAdjustmentRepository, PendingStockAdjustmentRepository>();
        }
    }
}
