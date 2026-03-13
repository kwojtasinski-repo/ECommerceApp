using ECommerceApp.Domain.Sales.Coupons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Sales.Coupons.Configurations
{
    internal sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
    {
        public void Configure(EntityTypeBuilder<Coupon> builder)
        {
            builder.ToTable("Coupons");

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id)
                   .HasConversion(x => x.Value, v => new CouponId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(c => c.Code)
                   .HasMaxLength(50)
                   .IsRequired();
            builder.HasIndex(c => c.Code).IsUnique();

            builder.Property(c => c.DiscountPercent).IsRequired();

            builder.Property(c => c.Description)
                   .HasMaxLength(500)
                   .IsRequired();

            builder.Property(c => c.Status)
                   .HasConversion<string>()
                   .HasMaxLength(20)
                   .IsRequired();
        }
    }
}
