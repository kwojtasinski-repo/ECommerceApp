using ECommerceApp.Domain.Catalog.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Catalog.Products.Configurations
{
    internal sealed class ItemTagConfiguration : IEntityTypeConfiguration<ItemTag>
    {
        public void Configure(EntityTypeBuilder<ItemTag> builder)
        {
            builder.ToTable("ItemTags");

            builder.HasKey(it => new { it.ItemId, it.TagId });

            builder.Property(it => it.ItemId)
                   .HasConversion(x => x.Value, v => new ItemId(v));

            builder.Property(it => it.TagId)
                   .HasConversion(x => x.Value, v => new TagId(v));

            builder.HasIndex(it => it.TagId);
        }
    }
}
