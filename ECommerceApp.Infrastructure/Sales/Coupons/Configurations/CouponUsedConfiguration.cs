using ECommerceApp.Domain.Sales.Coupons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Sales.Coupons.Configurations
{
    internal sealed class CouponUsedConfiguration : IEntityTypeConfiguration<CouponUsed>
    {
        public void Configure(EntityTypeBuilder<CouponUsed> builder)
        {
            builder.ToTable("CouponUsed");

            builder.HasKey(cu => cu.Id);
            builder.Property(cu => cu.Id)
                   .HasConversion(x => x.Value, v => new CouponUsedId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(cu => cu.CouponId)
                   .HasConversion(x => x.Value, v => new CouponId(v))
                   .IsRequired();
            builder.HasIndex(cu => cu.CouponId).IsUnique();

            builder.Property(cu => cu.OrderId).IsRequired();
            builder.HasIndex(cu => cu.OrderId).IsUnique();

            builder.Property(cu => cu.UsedAt).IsRequired();
        }
    }
}
