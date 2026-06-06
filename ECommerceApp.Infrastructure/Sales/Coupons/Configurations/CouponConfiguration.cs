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

            builder.OwnsOne(c => c.Code, code =>
            {
                code.Property(x => x.Value)
                    .HasColumnName(nameof(Coupon.Code))
                    .HasMaxLength(50)
                    .IsRequired();
                code.HasIndex(x => x.Value)
                    .HasDatabaseName("IX_Coupons_Code")
                    .IsUnique()
                    .HasFilter(null);
            });

            builder.Navigation(c => c.Code).IsRequired();

            builder.OwnsOne(c => c.Description, description =>
            {
                description.Property(x => x.Value)
                           .HasColumnName(nameof(Coupon.Description))
                           .HasMaxLength(500)
                           .IsRequired();
            });

            builder.Property(c => c.Status)
                   .HasConversion<string>()
                   .HasMaxLength(20)
                   .IsRequired();

            builder.Navigation(c => c.Description).IsRequired();

            // ── Slice 2 ─────────────────────────────────────────────────
            builder.Property(c => c.RulesJson)
                   .HasColumnType("nvarchar(max)");

            builder.Property(c => c.Version)
                   .IsRowVersion();

            builder.Property(c => c.BypassOversizeGuard)
                   .IsRequired()
                   .HasDefaultValue(false);
        }
    }
}
