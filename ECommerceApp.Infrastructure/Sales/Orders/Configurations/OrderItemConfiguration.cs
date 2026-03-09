using ECommerceApp.Domain.Sales.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Sales.Orders.Configurations
{
    internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
    {
        public void Configure(EntityTypeBuilder<OrderItem> builder)
        {
            builder.ToTable("OrderItems");

            builder.HasKey(oi => oi.Id);
            builder.Property(oi => oi.Id)
                   .HasConversion(x => x.Value, v => new OrderItemId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(oi => oi.ItemId).IsRequired();

            builder.Property(oi => oi.Quantity).IsRequired();

            builder.Property(oi => oi.UnitCost)
                   .HasPrecision(18, 4)
                   .IsRequired();

            builder.Property(oi => oi.UserId)
                   .HasMaxLength(450)
                   .IsRequired();

            builder.Property(oi => oi.OrderId)
                   .IsRequired(false);

            builder.Property(oi => oi.CouponUsedId);
            builder.Property(oi => oi.RefundId);

            builder.HasIndex(oi => oi.UserId);
            builder.HasIndex(oi => oi.OrderId);
        }
    }
}
