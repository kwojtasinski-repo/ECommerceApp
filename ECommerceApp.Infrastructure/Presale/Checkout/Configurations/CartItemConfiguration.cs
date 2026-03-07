using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Presale.Checkout.Configurations
{
    internal sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
    {
        public void Configure(EntityTypeBuilder<CartItem> builder)
        {
            builder.ToTable("CartItems");

            builder.HasKey(i => i.Id);
            builder.Property(i => i.Id)
                   .HasConversion(x => x.Value, v => new CartItemId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(i => i.CartId)
                   .HasConversion(x => x.Value, v => new CartId(v))
                   .IsRequired();

            builder.Property(i => i.ProductId)
                   .IsRequired();

            builder.Property(i => i.Quantity)
                   .IsRequired();

            builder.Property(i => i.UnitPrice)
                   .HasPrecision(18, 2)
                   .IsRequired();

            builder.HasIndex(i => new { i.CartId, i.ProductId });
        }
    }
}
