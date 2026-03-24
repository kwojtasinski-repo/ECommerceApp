using ECommerceApp.Domain.Sales.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Sales.Payments.Configurations
{
    internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.ToTable("Payments");

            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id)
                   .HasConversion(x => x.Value, v => new PaymentId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(p => p.OrderId)
                   .HasConversion(x => x.Value, v => new PaymentOrderId(v))
                   .IsRequired();
            builder.HasIndex(p => p.OrderId).IsUnique();

            builder.Property(p => p.TotalAmount)
                   .HasPrecision(18, 4)
                   .IsRequired();

            builder.Property(p => p.CurrencyId).IsRequired();

            builder.Property(p => p.Status)
                   .HasConversion<string>()
                   .HasMaxLength(20)
                   .IsRequired();

            builder.Property(p => p.ExpiresAt).IsRequired();
            builder.Property(p => p.ConfirmedAt);
            builder.Property(p => p.TransactionRef).HasMaxLength(200);

            builder.Property(p => p.PaymentId)
                   .IsRequired()
                   .HasDefaultValueSql("NEWID()");
            builder.HasIndex(p => p.PaymentId).IsUnique();

            builder.Property(p => p.UserId).HasMaxLength(450).IsRequired();
            builder.HasIndex(p => p.UserId);

            builder.Property(p => p.RowVersion)
                   .IsRowVersion();
        }
    }
}
