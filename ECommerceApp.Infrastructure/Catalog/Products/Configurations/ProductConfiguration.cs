using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Catalog.Products.ValueObjects;
using ECommerceApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Catalog.Products.Configurations
{
    internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("Products");

            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id)
                   .HasConversion(x => x.Value, v => new ProductId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(p => p.Name)
                   .HasConversion(x => x.Value, v => new ProductName(v))
                   .HasMaxLength(150)
                   .IsRequired();

            builder.Property(p => p.Cost)
                   .HasConversion(x => x.Amount, v => new Price(v))
                   .HasPrecision(18, 4)
                   .IsRequired();

            builder.Property(p => p.Description)
                   .HasConversion(x => x.Value, v => new ProductDescription(v))
                   .HasMaxLength(300)
                   .IsRequired();

            builder.Property(p => p.Quantity)
                   .HasConversion(x => x.Value, v => new ProductQuantity(v))
                   .IsRequired();

            builder.Property(p => p.Status)
                   .IsRequired()
                   .HasDefaultValue(ProductStatus.Draft);

            builder.Property(p => p.CategoryId)
                   .HasConversion(x => x.Value, v => new CategoryId(v))
                   .IsRequired();

            builder.HasMany(p => p.Images)
                   .WithOne()
                   .HasForeignKey(img => img.ProductId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(p => p.ProductTags)
                   .WithOne()
                   .HasForeignKey(pt => pt.ProductId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(p => p.Images)
                   .HasField("_images")
                   .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.Navigation(p => p.ProductTags)
                   .HasField("_productTags")
                   .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasIndex(p => p.CategoryId);
            builder.HasIndex(p => p.Status);
        }
    }
}
