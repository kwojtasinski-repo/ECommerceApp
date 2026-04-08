using ECommerceApp.Domain.Sales.Coupons;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Sales.Coupons
{
    internal sealed class CouponsDbContext : DbContext, ICouponsDbContext
    {
        public DbSet<Coupon> Coupons => Set<Coupon>();
        public DbSet<CouponUsed> CouponUsed => Set<CouponUsed>();
        public DbSet<CouponScopeTarget> CouponScopeTargets => Set<CouponScopeTarget>();
        public DbSet<CouponApplicationRecord> CouponApplicationRecords => Set<CouponApplicationRecord>();
        public DbSet<SpecialEvent> SpecialEvents => Set<SpecialEvent>();

        public CouponsDbContext(DbContextOptions<CouponsDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema(CouponsConstants.SchemaName);
            modelBuilder.ApplyConfigurationsFromAssembly(
                GetType().Assembly,
                t => t.Namespace != null && t.Namespace.StartsWith("ECommerceApp.Infrastructure.Sales.Coupons.Configurations"));
            modelBuilder.UseUtcDateTimes();
        }
    }
}
