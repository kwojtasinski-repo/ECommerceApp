using ECommerceApp.Domain.AccountProfile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.AccountProfile.Configurations
{
    internal class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
    {
        public void Configure(EntityTypeBuilder<UserProfile> builder)
        {
            builder.ToTable("UserProfiles", UserProfileConstants.Schema);

            builder.HasKey(p => p.Id);

            builder.Property(p => p.UserId)
                   .HasMaxLength(450)
                   .IsRequired();

            builder.Property(p => p.FirstName)
                   .HasMaxLength(300)
                   .IsRequired();

            builder.Property(p => p.LastName)
                   .HasMaxLength(300)
                   .IsRequired();

            builder.Property(p => p.IsCompany)
                   .IsRequired();

            builder.Property(p => p.NIP)
                   .HasMaxLength(50);

            builder.Property(p => p.CompanyName)
                   .HasMaxLength(300);

            builder.Property(p => p.Email)
                   .HasMaxLength(300)
                   .IsRequired();

            builder.Property(p => p.PhoneNumber)
                   .HasMaxLength(50)
                   .IsRequired();

            builder.HasIndex(p => p.UserId).IsUnique();
            builder.HasIndex(p => p.NIP);

            builder.OwnsMany(p => p.Addresses, ab =>
            {
                ab.ToTable("Addresses", UserProfileConstants.Schema);
                ab.HasKey(a => a.Id);
                ab.Property(a => a.Street).HasMaxLength(300).IsRequired();
                ab.Property(a => a.BuildingNumber).HasMaxLength(150).IsRequired();
                ab.Property(a => a.ZipCode).IsRequired();
                ab.Property(a => a.City).HasMaxLength(300).IsRequired();
                ab.Property(a => a.Country).HasMaxLength(300).IsRequired();
                ab.HasIndex(a => a.ZipCode);
            });

            builder.Navigation(p => p.Addresses)
                   .HasField("_addresses")
                   .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
