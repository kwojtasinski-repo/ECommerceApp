using ECommerceApp.Domain.Inventory.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Inventory.Availability.Configurations
{
    internal sealed class PendingStockAdjustmentConfiguration : IEntityTypeConfiguration<PendingStockAdjustment>
    {
        public void Configure(EntityTypeBuilder<PendingStockAdjustment> builder)
        {
            builder.ToTable("PendingStockAdjustments");

            builder.HasKey(p => p.ProductId);

            builder.Property(p => p.NewQuantity).IsRequired();

            builder.Property(p => p.Version).IsRequired();

            builder.Property(p => p.SubmittedAt).IsRequired();
        }
    }
}
