using ECommerceApp.Domain.Catalog.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Catalog.Products.Configurations
{
    internal sealed class ProductTagConfiguration : IEntityTypeConfiguration<ProductTag>
    {
        public void Configure(EntityTypeBuilder<ProductTag> builder)
        {
            builder.ToTable("ProductTags");

            builder.HasKey(pt => new { pt.ProductId, pt.TagId });

            builder.Property(pt => pt.ProductId)
                   .HasConversion(x => x.Value, v => new ProductId(v));

            builder.Property(pt => pt.TagId)
                   .HasConversion(x => x.Value, v => new TagId(v));

            builder.HasIndex(pt => pt.TagId);
        }
    }
}
