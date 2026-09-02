using ECommerceApp.Domain.Sales.Coupons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sales.Coupons
{
    internal interface ICouponsDbContext
    {
        DbSet<Coupon> Coupons { get; }
        DbSet<CouponUsed> CouponUsed { get; }
        DbSet<CouponScopeTarget> CouponScopeTargets { get; }
        DbSet<CouponApplicationRecord> CouponApplicationRecords { get; }
        DbSet<SpecialEvent> SpecialEvents { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>Needed to point a concurrency token's original value at the version an edit began from.</summary>
        EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
    }
}
