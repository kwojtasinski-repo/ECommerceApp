using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Catalog.Products.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Catalog.Products.Configurations
{
    internal sealed class ImageConfiguration : IEntityTypeConfiguration<Image>
    {
        public void Configure(EntityTypeBuilder<Image> builder)
        {
            builder.ToTable("Images");

            builder.HasKey(i => i.Id);
            builder.Property(i => i.Id)
                   .HasConversion(x => x.Value, v => new ImageId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(i => i.FileName)
                   .HasConversion(x => x.Value, v => new ImageFileName(v))
                   .HasMaxLength(500)
                   .IsRequired();

            builder.Property(i => i.IsMain)
                   .IsRequired()
                   .HasDefaultValue(false);

            builder.Property(i => i.SortOrder)
                   .IsRequired()
                   .HasDefaultValue(0);

            builder.Property(i => i.ProductId)
                   .HasConversion(x => x.Value, v => new ProductId(v))
                   .IsRequired();

            builder.HasIndex(i => i.ProductId);
        }
    }
}
