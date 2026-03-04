using ECommerceApp.Domain.Inventory.Availability;
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
                   .IsRequired();

            builder.HasIndex(s => s.ProductId).IsUnique();

            builder.Property(s => s.Quantity)
                   .IsRequired();

            builder.Property(s => s.ReservedQuantity)
                   .IsRequired()
                   .HasDefaultValue(0);

            builder.Property(s => s.RowVersion)
                   .IsRowVersion();
        }
    }
}
