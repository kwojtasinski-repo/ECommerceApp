using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Database.Configurations
{
    internal class ItemConfiguration : IEntityTypeConfiguration<Item>
    {
        public void Configure(EntityTypeBuilder<Item> builder)
        {
            builder.HasKey(i => i.Id);

            builder.Property(i => i.Name)
                   .HasMaxLength(150)
                   .IsRequired();
            
            builder.Property(i => i.Description)
                   .HasMaxLength(300)
                   .IsRequired();
            
            builder.Property(i => i.Cost)
                   .IsRequired()
                   .HasDefaultValue(0)
                   .HasPrecision(18, 4);

            builder.Property(i => i.Quantity)
                   .IsRequired();

            builder.Property(i => i.Warranty)
                   .HasMaxLength(50)
                   .IsRequired();

            builder
                .HasMany(i => i.Images)
                .WithOne(it => it.Item)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
