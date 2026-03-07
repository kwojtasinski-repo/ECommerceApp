using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Presale.Checkout.Configurations
{
    internal sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
    {
        public void Configure(EntityTypeBuilder<Cart> builder)
        {
            builder.ToTable("Carts");

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id)
                   .HasConversion(x => x.Value, v => new CartId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(c => c.UserId)
                   .HasMaxLength(450)
                   .IsRequired();

            builder.HasIndex(c => c.UserId).IsUnique();

            builder.HasMany(c => c.Items)
                   .WithOne()
                   .HasForeignKey(i => i.CartId)
                   .IsRequired()
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(c => c.Items)
                   .HasField("_items")
                   .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
