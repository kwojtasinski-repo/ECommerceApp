using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Database.Configurations
{
    internal class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.HasKey(o => o.Id);

            builder.Property(o => o.Ordered)
                .IsRequired();

            builder.Property(o => o.CustomerId)
                .IsRequired();

            builder.Property(o => o.CurrencyId)
                .IsRequired();

            builder.Property(o => o.UserId)
                .IsRequired();

            builder.Property(o => o.Cost)
                .IsRequired()
                .HasDefaultValue(0)
                .HasPrecision(18, 4);

            builder.Property(o => o.Number)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(p => p.Number).IsUnique();

            builder
                .HasOne(o => o.CouponUsed)
                .WithOne(c => c.Order)
                .HasForeignKey<CouponUsed>(c => c.OrderId);
        }
    }
}
