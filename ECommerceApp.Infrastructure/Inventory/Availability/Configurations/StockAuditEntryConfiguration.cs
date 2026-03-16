using ECommerceApp.Domain.Inventory.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Inventory.Availability.Configurations
{
    internal sealed class StockAuditEntryConfiguration : IEntityTypeConfiguration<StockAuditEntry>
    {
        public void Configure(EntityTypeBuilder<StockAuditEntry> builder)
        {
            builder.ToTable("StockAuditEntries");

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id)
                   .HasConversion(x => x.Value, v => new StockAuditEntryId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(e => e.ProductId).IsRequired();

            builder.Property(e => e.ChangeType)
                   .HasColumnType("tinyint")
                   .IsRequired();

            builder.Property(e => e.QuantityBefore).IsRequired();
            builder.Property(e => e.QuantityAfter).IsRequired();
            builder.Property(e => e.OrderId);
            builder.Property(e => e.OccurredAt).IsRequired();

            builder.Ignore(e => e.Delta);

            builder.HasIndex(e => e.OccurredAt)
                   .IsDescending()
                   .IncludeProperties(
                       nameof(StockAuditEntry.ProductId),
                       nameof(StockAuditEntry.ChangeType),
                       nameof(StockAuditEntry.QuantityBefore),
                       nameof(StockAuditEntry.QuantityAfter));

            builder.HasIndex(e => new { e.ProductId, e.OccurredAt })
                   .IsDescending(false, true);
        }
    }
}
