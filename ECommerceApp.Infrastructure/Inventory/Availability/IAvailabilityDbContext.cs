using ECommerceApp.Domain.Inventory.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Inventory.Availability
{
    internal interface IAvailabilityDbContext
    {
        DbSet<StockItem> StockItems { get; }
        DbSet<StockHold> StockHolds { get; }
        DbSet<ProductSnapshot> ProductSnapshots { get; }
        DbSet<PendingStockAdjustment> PendingStockAdjustments { get; }
        DbSet<StockAuditEntry> StockAuditEntries { get; }
        EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
