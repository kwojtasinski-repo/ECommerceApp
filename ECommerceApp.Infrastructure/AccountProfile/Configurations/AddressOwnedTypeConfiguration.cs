using ECommerceApp.Domain.AccountProfile;
using ECommerceApp.Domain.AccountProfile.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.AccountProfile.Configurations
{
    internal sealed class AddressOwnedTypeConfiguration
    {
        public void Configure(OwnedNavigationBuilder<UserProfile, Address> ab)
        {
            ab.ToTable("Addresses");

            ab.HasKey(a => a.Id);
            ab.Property(a => a.Id)
              .HasConversion(x => x.Value, v => new AddressId(v))
              .ValueGeneratedOnAdd();

            ab.Property(a => a.Street)
              .HasConversion(x => x.Value, v => new Street(v))
              .HasMaxLength(300)
              .IsRequired();

            ab.Property(a => a.BuildingNumber)
              .HasConversion(x => x.Value, v => new BuildingNumber(v))
              .HasMaxLength(150)
              .IsRequired();

            ab.Property(a => a.FlatNumber)
              .HasConversion(x => x!.Value, v => new FlatNumber(v));

            ab.Property(a => a.ZipCode)
              .HasConversion(x => x.Value, v => new ZipCode(v))
              .HasMaxLength(12)
              .IsRequired();

            ab.Property(a => a.City)
              .HasConversion(x => x.Value, v => new City(v))
              .HasMaxLength(300)
              .IsRequired();

            ab.Property(a => a.Country)
              .HasConversion(x => x.Value, v => new Country(v))
              .HasMaxLength(300)
              .IsRequired();

            ab.HasIndex(a => a.ZipCode);
        }
    }
}
