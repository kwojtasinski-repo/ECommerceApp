using ECommerceApp.Domain.Inventory.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Inventory.Availability.Configurations
{
    internal sealed class ProductSnapshotConfiguration : IEntityTypeConfiguration<ProductSnapshot>
    {
        public void Configure(EntityTypeBuilder<ProductSnapshot> builder)
        {
            builder.ToTable("ProductSnapshots");

            builder.HasKey(p => p.ProductId);

            builder.Property(p => p.ProductName)
                   .HasMaxLength(200)
                   .IsRequired();

            builder.Property(p => p.IsDigital).IsRequired();

            builder.Property(p => p.CatalogStatus)
                   .HasColumnType("tinyint")
                   .IsRequired();
        }
    }
}
