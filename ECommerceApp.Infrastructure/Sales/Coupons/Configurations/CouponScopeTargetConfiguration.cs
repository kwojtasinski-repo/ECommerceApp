using ECommerceApp.Domain.Sales.Coupons;
using ECommerceApp.Domain.Sales.Coupons.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Sales.Coupons.Configurations
{
    internal sealed class CouponScopeTargetConfiguration : IEntityTypeConfiguration<CouponScopeTarget>
    {
        public void Configure(EntityTypeBuilder<CouponScopeTarget> builder)
        {
            builder.ToTable("CouponScopeTargets");

            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id)
                   .HasConversion(x => x.Value, v => new CouponScopeTargetId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(t => t.CouponId)
                   .HasConversion(x => x.Value, v => new CouponId(v))
                   .IsRequired();
            builder.HasIndex(t => t.CouponId);

            builder.Property(t => t.ScopeType)
                   .HasConversion(x => x.Value, v => new CouponScopeType(v))
                   .HasMaxLength(50)
                   .IsRequired();

            builder.Property(t => t.TargetId)
                   .IsRequired();

            builder.Property(t => t.TargetName)
                   .HasMaxLength(200)
                   .IsRequired();
        }
    }
}
