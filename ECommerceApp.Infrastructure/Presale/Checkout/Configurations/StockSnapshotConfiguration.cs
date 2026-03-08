using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Presale.Checkout.Configurations
{
    internal sealed class StockSnapshotConfiguration : IEntityTypeConfiguration<StockSnapshot>
    {
        public void Configure(EntityTypeBuilder<StockSnapshot> builder)
        {
            builder.ToTable("StockSnapshots");

            builder.HasKey(e => e.ProductId);
            builder.Property(e => e.ProductId)
                   .HasConversion(id => id.Value, v => new PresaleProductId(v))
                   .ValueGeneratedNever();

            builder.HasIndex(e => e.ProductId);
        }
    }
}
