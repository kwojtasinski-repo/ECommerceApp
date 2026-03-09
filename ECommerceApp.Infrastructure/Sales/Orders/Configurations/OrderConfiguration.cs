using ECommerceApp.Domain.Sales.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Sales.Orders.Configurations
{
    internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.ToTable("Orders");

            builder.HasKey(o => o.Id);
            builder.Property(o => o.Id)
                   .HasConversion(x => x.Value, v => new OrderId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(o => o.Number)
                   .HasMaxLength(50)
                   .IsRequired();

            builder.Property(o => o.Cost)
                   .HasPrecision(18, 4)
                   .IsRequired();

            builder.Property(o => o.Ordered)
                   .IsRequired();

            builder.Property(o => o.Delivered);
            builder.Property(o => o.IsDelivered).IsRequired();
            builder.Property(o => o.IsPaid).IsRequired();
            builder.Property(o => o.DiscountPercent);

            builder.Property(o => o.CustomerId).IsRequired();
            builder.Property(o => o.CurrencyId).IsRequired();

            builder.Property(o => o.UserId)
                   .HasMaxLength(450)
                   .IsRequired();

            builder.Property(o => o.PaymentId);
            builder.Property(o => o.RefundId);
            builder.Property(o => o.CouponUsedId);

            builder.HasMany(o => o.OrderItems)
                   .WithOne()
                   .HasForeignKey(oi => oi.OrderId)
                   .IsRequired(false)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(o => o.OrderItems)
                   .HasField("_orderItems")
                   .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasIndex(o => o.UserId);
            builder.HasIndex(o => o.CustomerId);
            builder.HasIndex(o => o.IsPaid);
        }
    }
}
