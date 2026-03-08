using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Presale.Checkout.Configurations
{
    internal sealed class CartLineConfiguration : IEntityTypeConfiguration<CartLine>
    {
        public void Configure(EntityTypeBuilder<CartLine> builder)
        {
            builder.ToTable("CartLines");

            builder.HasKey(e => new { e.UserId, e.ProductId });

            builder.Property(e => e.UserId)
                   .HasConversion(id => id.Value, v => new PresaleUserId(v))
                   .HasMaxLength(450);

            builder.Property(e => e.ProductId)
                   .HasConversion(id => id.Value, v => new PresaleProductId(v));

            builder.Property(e => e.Quantity)
                   .HasConversion(q => q.Value, v => new Quantity(v))
                   .IsRequired();
        }
    }
}
