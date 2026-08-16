using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Presale.Checkout.Configurations
{
    internal sealed class OrderAccessTokenConfiguration : IEntityTypeConfiguration<OrderAccessToken>
    {
        public void Configure(EntityTypeBuilder<OrderAccessToken> builder)
        {
            builder.ToTable("OrderAccessTokens");

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id)
                   .ValueGeneratedOnAdd();

            builder.Property(e => e.OrderId)
                   .IsRequired();

            builder.Property(e => e.UserProfileId)
                   .IsRequired();

            builder.Property(e => e.Token)
                   .HasMaxLength(100)
                   .IsRequired();

            builder.HasIndex(e => e.Token)
                   .IsUnique();

            builder.Property(e => e.CreatedAt)
                   .IsRequired();
        }
    }
}
