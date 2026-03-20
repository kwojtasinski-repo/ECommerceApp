using ECommerceApp.Domain.Sales.Coupons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Sales.Coupons.Configurations
{
    internal sealed class CouponApplicationRecordConfiguration : IEntityTypeConfiguration<CouponApplicationRecord>
    {
        public void Configure(EntityTypeBuilder<CouponApplicationRecord> builder)
        {
            builder.ToTable("CouponApplicationRecords");

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id)
                   .HasConversion(x => x.Value, v => new CouponApplicationRecordId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(r => r.CouponUsedId).IsRequired();
            builder.HasIndex(r => r.CouponUsedId);

            builder.Property(r => r.CouponCode)
                   .HasMaxLength(50)
                   .IsRequired();

            builder.Property(r => r.DiscountType)
                   .HasMaxLength(50)
                   .IsRequired();

            builder.Property(r => r.DiscountValue)
                   .HasPrecision(18, 2)
                   .IsRequired();

            builder.Property(r => r.OriginalTotal)
                   .HasPrecision(18, 2)
                   .IsRequired();

            builder.Property(r => r.Reduction)
                   .HasPrecision(18, 2)
                   .IsRequired();

            builder.Property(r => r.AppliedAt).IsRequired();
            builder.Property(r => r.WasReversed).IsRequired();
        }
    }
}
