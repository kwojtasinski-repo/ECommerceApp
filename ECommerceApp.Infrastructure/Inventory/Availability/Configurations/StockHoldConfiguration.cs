using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Domain.Inventory.Availability.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Inventory.Availability.Configurations
{
    internal sealed class StockHoldConfiguration : IEntityTypeConfiguration<StockHold>
    {
        public void Configure(EntityTypeBuilder<StockHold> builder)
        {
            builder.ToTable("StockHolds");

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id)
                   .HasConversion(x => x.Value, v => new StockHoldId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(r => r.ProductId)
                   .HasConversion(x => x.Value, v => new StockProductId(v))
                   .IsRequired();

            builder.Property(r => r.OrderId)
                   .HasConversion(x => x.Value, v => new ReservationOrderId(v))
                   .IsRequired();

            builder.Property(r => r.Quantity).IsRequired();

            builder.Property(r => r.Status)
                   .HasColumnType("tinyint")
                   .IsRequired();

            builder.Property(r => r.ReservedAt).IsRequired();
            builder.Property(r => r.ExpiresAt).IsRequired();

            var guaranteedStatus = (byte)StockHoldStatus.Guaranteed;
            var confirmedStatus = (byte)StockHoldStatus.Confirmed;
            var activeFilter = $"[Status] IN ({guaranteedStatus}, {confirmedStatus})";

            builder.HasIndex(r => new { r.OrderId, r.ProductId })
                   .HasFilter(activeFilter);

            builder.HasIndex(r => r.ReservedAt)
                   .IsDescending()
                   .IncludeProperties(
                       nameof(StockHold.ProductId),
                       nameof(StockHold.OrderId),
                       nameof(StockHold.Quantity),
                       nameof(StockHold.ExpiresAt))
                   .HasFilter(activeFilter);
        }
    }
}
