using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Presale.Checkout.Configurations
{
    internal sealed class SoftReservationConfiguration : IEntityTypeConfiguration<SoftReservation>
    {
        public void Configure(EntityTypeBuilder<SoftReservation> builder)
        {
            builder.ToTable("SoftReservations");

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id)
                   .HasConversion(id => id.Value, v => new SoftReservationId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(e => e.ProductId)
                   .HasConversion(id => id.Value, v => new PresaleProductId(v))
                   .IsRequired();

            builder.Property(e => e.UserId)
                   .HasConversion(id => id.Value, v => new PresaleUserId(v))
                   .HasMaxLength(450)
                   .IsRequired();

            builder.Property(e => e.Quantity)
                   .HasConversion(q => q.Value, v => new Quantity(v))
                   .IsRequired();

            builder.Property(e => e.UnitPrice)
                   .HasConversion(p => p.Amount, v => new Price(v))
                   .HasColumnType("decimal(18,2)")
                   .IsRequired();

            builder.HasIndex(e => new { e.ProductId, e.UserId }).IsUnique();

            builder.Property(e => e.Status)
                   .HasConversion<int>()
                   .HasDefaultValue(SoftReservationStatus.Active)
                   .IsRequired();
        }
    }
}
