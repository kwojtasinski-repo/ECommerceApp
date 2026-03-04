using ECommerceApp.Domain.Inventory.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Inventory.Availability.Configurations
{
    internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
    {
        public void Configure(EntityTypeBuilder<Reservation> builder)
        {
            builder.ToTable("Reservations");

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id)
                   .HasConversion(x => x.Value, v => new ReservationId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(r => r.ProductId).IsRequired();
            builder.Property(r => r.OrderId).IsRequired();
            builder.Property(r => r.Quantity).IsRequired();

            builder.Property(r => r.Status)
                   .HasColumnType("tinyint")
                   .IsRequired();

            builder.Property(r => r.ReservedAt).IsRequired();
            builder.Property(r => r.ExpiresAt).IsRequired();

            builder.HasIndex(r => new { r.OrderId, r.ProductId });
        }
    }
}
