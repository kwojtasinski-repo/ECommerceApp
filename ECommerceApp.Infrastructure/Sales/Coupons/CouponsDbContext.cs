using ECommerceApp.Domain.Sales.Coupons;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Sales.Coupons
{
    internal sealed class CouponsDbContext : DbContext
    {
        public DbSet<Coupon> Coupons => Set<Coupon>();
        public DbSet<CouponUsed> CouponUsed => Set<CouponUsed>();

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
        }
    }
}
