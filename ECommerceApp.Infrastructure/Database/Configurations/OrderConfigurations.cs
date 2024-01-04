using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Database.Configurations
{
    internal class OrderConfigurations : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Ordered)
                .IsRequired();

            builder.Property(p => p.CustomerId)
                .IsRequired();

            builder.Property(p => p.CurrencyId)
                .IsRequired();

            builder.Property(p => p.UserId)
                .IsRequired();

            builder.Property(p => p.Cost)
                .IsRequired()
                .HasDefaultValue(0)
                .HasPrecision(18, 4);

            builder.Property(p => p.Number)
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
