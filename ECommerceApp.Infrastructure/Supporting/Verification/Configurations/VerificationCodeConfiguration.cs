using ECommerceApp.Domain.Supporting.Verification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Supporting.Verification.Configurations
{
    internal sealed class VerificationCodeConfiguration : IEntityTypeConfiguration<VerificationCode>
    {
        public void Configure(EntityTypeBuilder<VerificationCode> builder)
        {
            builder.ToTable("VerificationCodes");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id)
                   .ValueGeneratedOnAdd();

            builder.Property(x => x.Purpose)
                   .IsRequired();

            builder.Property(x => x.SubjectKey)
                   .HasMaxLength(200)
                   .IsRequired();

            builder.Property(x => x.Code)
                   .HasMaxLength(64)
                   .IsRequired();

            builder.Property(x => x.ExpiresAt)
                   .IsRequired();

            builder.Property(x => x.ConsumedAt);

            builder.HasIndex(x => new { x.Code, x.Purpose })
                   .IsUnique();
        }
    }
}