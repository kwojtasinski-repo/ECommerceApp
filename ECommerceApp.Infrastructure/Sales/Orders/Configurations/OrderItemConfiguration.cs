using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Shared;
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

            builder.Property(oi => oi.ItemId)
                   .HasConversion(id => id.Value, v => new OrderProductId(v))
                   .IsRequired();

            builder.Property(oi => oi.Quantity).IsRequired();

            builder.Property(oi => oi.UnitCost)
                   .HasConversion(uc => uc.Amount, v => new UnitCost(v))
                   .HasPrecision(18, 4)
                   .IsRequired();

            builder.Property(oi => oi.UserId)
                   .HasConversion(id => id.Value, v => new OrderUserId(v))
                   .HasMaxLength(450)
                   .IsRequired();

            builder.Property(oi => oi.OrderId)
                   .HasConversion(
                       id => id != null ? (int?)id.Value : null,
                       v => v.HasValue ? new OrderId(v.Value) : null)
                   .IsRequired(false);

            builder.Property(oi => oi.CouponUsedId);

            builder.OwnsOne(oi => oi.Snapshot, s =>
            {
                s.ToTable("OrderItemSnapshots");
                s.WithOwner().HasForeignKey("OrderItemId");
                s.Property(p => p.ProductName)
                 .HasMaxLength(300)
                 .IsRequired();
                s.Property(p => p.ImageFileName)
                 .HasMaxLength(255);
                s.Property(p => p.ImageUrl)
                 .HasMaxLength(2048);
            });

            builder.HasIndex(oi => oi.UserId);
            builder.HasIndex(oi => oi.OrderId);
        }
    }
}
