using ECommerceApp.Domain.AccountProfile;
using ECommerceApp.Domain.AccountProfile.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.AccountProfile.Configurations
{
    internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
    {
        public void Configure(EntityTypeBuilder<UserProfile> builder)
        {
            builder.ToTable("UserProfiles");

            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id)
                   .HasConversion(x => x.Value, v => new UserProfileId(v))
                   .ValueGeneratedOnAdd();

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
                   .HasMaxLength(50)
                   .HasConversion(x => x == null ? null : x.Value, v => v == null ? null : new Nip(v));

            builder.Property(p => p.CompanyName)
                   .HasMaxLength(300)
                   .HasConversion(x => x == null ? null : x.Value, v => v == null ? null : new CompanyName(v));

            builder.Property(p => p.Email)
                   .HasMaxLength(300)
                   .IsRequired()
                   .HasConversion(x => x.Value, v => new Email(v));

            builder.Property(p => p.PhoneNumber)
                   .HasMaxLength(50)
                   .IsRequired()
                   .HasConversion(x => x.Value, v => new PhoneNumber(v));

            builder.HasIndex(p => p.UserId).IsUnique();
            builder.HasIndex(p => p.NIP);

            builder.OwnsMany(p => p.Addresses, new AddressOwnedTypeConfiguration().Configure);

            builder.Navigation(p => p.Addresses)
                   .HasField("_addresses")
                   .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
