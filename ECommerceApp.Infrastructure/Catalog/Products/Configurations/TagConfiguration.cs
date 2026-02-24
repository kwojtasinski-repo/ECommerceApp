using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Catalog.Products.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Catalog.Products.Configurations
{
    internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
    {
        public void Configure(EntityTypeBuilder<Tag> builder)
        {
            builder.ToTable("Tags");

            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id)
                   .HasConversion(x => x.Value, v => new TagId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(t => t.Name)
                   .HasConversion(x => x.Value, v => new TagName(v))
                   .HasMaxLength(50)
                   .IsRequired();

            builder.Property(t => t.Slug)
                   .HasConversion(x => x.Value, v => new TagSlug(v))
                   .HasMaxLength(30)
                   .IsRequired();

            builder.HasIndex(t => t.Slug).IsUnique();
        }
    }
}
