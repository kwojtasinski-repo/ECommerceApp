using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Catalog.Products.ValueObjects;
using ECommerceApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Catalog.Products.Configurations
{
    internal sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
    {
        public void Configure(EntityTypeBuilder<Item> builder)
        {
            builder.ToTable("Items");

            builder.HasKey(i => i.Id);
            builder.Property(i => i.Id)
                   .HasConversion(x => x.Value, v => new ItemId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(i => i.Name)
                   .HasConversion(x => x.Value, v => new ProductName(v))
                   .HasMaxLength(150)
                   .IsRequired();

            builder.Property(i => i.Cost)
                   .HasConversion(x => x.Amount, v => new Price(v))
                   .HasPrecision(18, 4)
                   .IsRequired();

            builder.Property(i => i.Description)
                   .HasConversion(x => x.Value, v => new ProductDescription(v))
                   .HasMaxLength(300)
                   .IsRequired()
                   .HasDefaultValue("");

            builder.Property(i => i.Quantity)
                   .HasConversion(x => x.Value, v => new ProductQuantity(v))
                   .IsRequired();

            builder.Property(i => i.Status)
                   .IsRequired()
                   .HasDefaultValue(ProductStatus.Draft);

            builder.Property(i => i.CategoryId)
                   .HasConversion(x => x.Value, v => new CategoryId(v))
                   .IsRequired();

            builder.HasMany(i => i.Images)
                   .WithOne()
                   .HasForeignKey(img => img.ItemId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(i => i.ItemTags)
                   .WithOne()
                   .HasForeignKey(it => it.ItemId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(i => i.Images)
                   .HasField("_images")
                   .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.Navigation(i => i.ItemTags)
                   .HasField("_itemTags")
                   .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasIndex(i => i.CategoryId);
            builder.HasIndex(i => i.Status);
        }
    }
}
