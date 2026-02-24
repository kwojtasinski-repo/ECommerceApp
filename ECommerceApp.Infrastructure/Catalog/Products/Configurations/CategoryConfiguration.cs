using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Catalog.Products.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Catalog.Products.Configurations
{
    internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> builder)
        {
            builder.ToTable("Categories");

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id)
                   .HasConversion(x => x.Value, v => new CategoryId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(c => c.Name)
                   .HasConversion(x => x.Value, v => new CategoryName(v))
                   .HasMaxLength(100)
                   .IsRequired();

            builder.Property(c => c.Slug)
                   .HasConversion(x => x.Value, v => new Slug(v))
                   .HasMaxLength(100)
                   .IsRequired();

            builder.HasIndex(c => c.Slug).IsUnique();
        }
    }
}
