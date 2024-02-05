using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Database.Configurations
{
    internal class CouponConfiguration : IEntityTypeConfiguration<Coupon>
    {
        public void Configure(EntityTypeBuilder<Coupon> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Code)
                   .HasMaxLength(150)
                   .IsRequired();

            builder.Property(c => c.Discount)
                   .IsRequired();

            builder.Property(c => c.Description)
                   .HasMaxLength(300)
                   .IsRequired();

            builder.HasIndex(c => c.Code).IsUnique();
            builder.HasIndex(c => new { c.Id, c.Code });

            builder
                .HasOne(c => c.CouponUsed)
                .WithOne(c => c.Coupon)
                .HasForeignKey<CouponUsed>(c => c.CouponId);
        }
    }
}
