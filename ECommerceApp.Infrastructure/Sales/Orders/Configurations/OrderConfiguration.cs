using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
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
                   .HasConversion(n => n.Value, v => OrderNumber.Parse(v))
                   .HasColumnType("varchar(25)")
                   .HasMaxLength(25)
                   .IsRequired();
            builder.HasIndex(o => o.Number).IsUnique();

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
                   .HasConversion(id => id.Value, v => new OrderUserId(v))
                   .HasMaxLength(450)
                   .IsRequired();

            builder.Property(o => o.PaymentId);
            builder.Property(o => o.RefundId);
            builder.Property(o => o.CouponUsedId);

            builder.OwnsOne(o => o.Customer, c =>
            {
                c.ToTable("OrderCustomers");
                c.WithOwner().HasForeignKey("OrderId");
                c.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
                c.Property(p => p.LastName).HasMaxLength(100).IsRequired();
                c.Property(p => p.Email).HasMaxLength(256).IsRequired();
                c.Property(p => p.PhoneNumber).HasMaxLength(50).IsRequired();
                c.Property(p => p.IsCompany).IsRequired();
                c.Property(p => p.CompanyName).HasMaxLength(300);
                c.Property(p => p.Nip).HasMaxLength(20);
                c.Property(p => p.Street).HasMaxLength(200).IsRequired();
                c.Property(p => p.BuildingNumber).HasMaxLength(20).IsRequired();
                c.Property(p => p.FlatNumber).HasMaxLength(20);
                c.Property(p => p.ZipCode).HasMaxLength(20).IsRequired();
                c.Property(p => p.City).HasMaxLength(100).IsRequired();
                c.Property(p => p.Country).HasMaxLength(100).IsRequired();
            });

            builder.HasMany(o => o.OrderItems)
                   .WithOne()
                   .HasForeignKey(oi => oi.OrderId)
                   .IsRequired(false)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(o => o.OrderItems)
                   .HasField("_orderItems")
                   .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(o => o.Events)
                   .WithOne()
                   .HasForeignKey(e => e.OrderId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(o => o.Events)
                   .HasField("_events")
                   .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasIndex(o => o.UserId);
            builder.HasIndex(o => o.CustomerId);
            builder.HasIndex(o => o.IsPaid);
        }
    }
}
