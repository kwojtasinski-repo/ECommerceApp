using ECommerceApp.Domain.Profiles.AccountProfile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Profiles.AccountProfile.Configurations
{
    internal class AddressConfiguration : IEntityTypeConfiguration<Address>
    {
        public void Configure(EntityTypeBuilder<Address> builder)
        {
            builder.ToTable("Addresses", AccountProfileConstants.Schema);

            builder.HasKey(a => a.Id);

            builder.Property(a => a.Street)
                   .HasMaxLength(300)
                   .IsRequired();

            builder.Property(a => a.BuildingNumber)
                   .HasMaxLength(150)
                   .IsRequired();

            builder.Property(a => a.ZipCode)
                   .IsRequired();

            builder.Property(a => a.City)
                   .HasMaxLength(300)
                   .IsRequired();

            builder.Property(a => a.Country)
                   .HasMaxLength(300)
                   .IsRequired();

            builder.Property(a => a.AccountProfileId)
                   .IsRequired();

            builder.HasIndex(a => a.ZipCode);
            builder.HasIndex(a => a.AccountProfileId);
        }
    }
}
