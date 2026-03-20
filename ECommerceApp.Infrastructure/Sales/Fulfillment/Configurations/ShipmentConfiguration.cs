using ECommerceApp.Domain.Sales.Fulfillment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Sales.Fulfillment.Configurations
{
    internal sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
    {
        public void Configure(EntityTypeBuilder<Shipment> builder)
        {
            builder.ToTable("Shipments");

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id)
                   .HasConversion(x => x.Value, v => new ShipmentId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(s => s.OrderId).IsRequired();

            builder.Property(s => s.TrackingNumber)
                   .HasMaxLength(100);

            builder.Property(s => s.Status)
                   .HasConversion<string>()
                   .HasMaxLength(30)
                   .IsRequired();

            builder.Property(s => s.ShippedAt);
            builder.Property(s => s.DeliveredAt);

            builder.OwnsMany(s => s.Lines, line =>
            {
                line.ToTable("ShipmentLines");
                line.WithOwner().HasForeignKey("ShipmentId");
                line.HasKey(l => l.Id);
                line.Property(l => l.Id).ValueGeneratedOnAdd();
                line.Property(l => l.ProductId).IsRequired();
                line.Property(l => l.Quantity).IsRequired();
            });

            builder.Navigation(s => s.Lines)
                   .HasField("_lines")
                   .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
