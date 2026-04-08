using ECommerceApp.Domain.Sales.Coupons;
using Microsoft.EntityFrameworkCore;
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
    }
}
