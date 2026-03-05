using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Domain.Inventory.Availability.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Inventory.Availability.Configurations
{
    internal sealed class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
    {
        public void Configure(EntityTypeBuilder<StockItem> builder)
        {
            builder.ToTable("StockItems");

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id)
                   .HasConversion(x => x.Value, v => new StockItemId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(s => s.ProductId)
                   .HasConversion(x => x.Value, v => new StockProductId(v))
                   .IsRequired();

            builder.HasIndex(s => s.ProductId).IsUnique();

            builder.Property(s => s.Quantity)
                   .HasConversion(x => x.Value, v => new StockQuantity(v))
                   .IsRequired();

            builder.Property(s => s.ReservedQuantity)
                   .HasConversion(x => x.Value, v => new StockQuantity(v))
                   .IsRequired()
                   .HasDefaultValue(0);

            builder.Property(s => s.RowVersion)
                   .IsRowVersion();
        }
    }
}
