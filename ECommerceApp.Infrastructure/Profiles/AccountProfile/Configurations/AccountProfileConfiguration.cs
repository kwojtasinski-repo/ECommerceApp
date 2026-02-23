using ECommerceApp.Domain.Profiles.AccountProfile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Profiles.AccountProfile.Configurations
{
    internal class AccountProfileConfiguration : IEntityTypeConfiguration<global::ECommerceApp.Domain.Profiles.AccountProfile.AccountProfile>
    {
        public void Configure(EntityTypeBuilder<global::ECommerceApp.Domain.Profiles.AccountProfile.AccountProfile> builder)
        {
            builder.ToTable("AccountProfiles", AccountProfileConstants.Schema);

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

            builder.HasIndex(p => p.UserId);
            builder.HasIndex(p => p.NIP);

            builder.HasMany(p => p.Addresses)
                   .WithOne()
                   .HasForeignKey(a => a.AccountProfileId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(p => p.Addresses)
                   .HasField("_addresses")
                   .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(p => p.ContactDetails)
                   .WithOne()
                   .HasForeignKey(c => c.AccountProfileId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(p => p.ContactDetails)
                   .HasField("_contactDetails")
                   .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
