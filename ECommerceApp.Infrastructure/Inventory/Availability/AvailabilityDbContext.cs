using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Inventory.Availability
{
    internal sealed class AvailabilityDbContext : DbContext, IAvailabilityDbContext
    {
        public DbSet<StockItem> StockItems => Set<StockItem>();
        public DbSet<StockHold> StockHolds => Set<StockHold>();
        public DbSet<ProductSnapshot> ProductSnapshots => Set<ProductSnapshot>();
        public DbSet<PendingStockAdjustment> PendingStockAdjustments => Set<PendingStockAdjustment>();
        public DbSet<StockAuditEntry> StockAuditEntries => Set<StockAuditEntry>();

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
