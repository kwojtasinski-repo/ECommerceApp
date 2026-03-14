using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Inventory.Availability
{
    public class AvailabilityDbContext : DbContext
    {
        public DbSet<StockItem> StockItems => Set<StockItem>();
        public DbSet<Reservation> Reservations => Set<Reservation>();
        public DbSet<ProductSnapshot> ProductSnapshots => Set<ProductSnapshot>();
        public DbSet<PendingStockAdjustment> PendingStockAdjustments => Set<PendingStockAdjustment>();

        public AvailabilityDbContext(DbContextOptions<AvailabilityDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema(AvailabilityConstants.SchemaName);
            modelBuilder.ApplyConfigurationsFromAssembly(
                GetType().Assembly,
                t => t.Namespace != null && t.Namespace.StartsWith("ECommerceApp.Infrastructure.Inventory.Availability.Configurations"));
            modelBuilder.UseUtcDateTimes();
        }
    }
}
