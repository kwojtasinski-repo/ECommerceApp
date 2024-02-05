using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Database.Configurations
{
    internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.DateOfOrderPayment)
                .IsRequired();

            builder.Property(p => p.State)
                .IsRequired()
                .HasMaxLength(50)
                .HasConversion<string>();

            builder.Property(p => p.Cost)
                .IsRequired()
                .HasDefaultValue(0)
                .HasPrecision(18, 4);

            builder.Property(p => p.Number)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(p => p.Number).IsUnique();

            builder
                .HasOne(p => p.Order)
                .WithOne(o => o.Payment)
                .HasForeignKey<Order>(o => o.PaymentId);
        }
    }
}
