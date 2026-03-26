using ECommerceApp.Domain.Identity.IAM;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Identity.IAM.Configurations
{
    internal class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.ToTable("RefreshTokens");

            builder.HasKey(r => r.Id);

            builder.Property(r => r.UserId)
                   .IsRequired()
                   .HasMaxLength(450);

            builder.Property(r => r.Token)
                   .IsRequired()
                   .HasMaxLength(256);

            builder.Property(r => r.JwtId)
                   .IsRequired()
                   .HasMaxLength(128);

            builder.HasIndex(r => r.Token)
                   .IsUnique();

            builder.HasIndex(r => r.UserId);
        }
    }
}
