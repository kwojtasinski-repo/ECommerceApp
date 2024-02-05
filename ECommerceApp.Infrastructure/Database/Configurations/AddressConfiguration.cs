using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Database.Configurations
{
    internal class AddressConfiguration : IEntityTypeConfiguration<Address>
    {
        public void Configure(EntityTypeBuilder<Address> builder)
        {
            builder.HasKey(a => a.Id);

            builder.Property(a => a.Street)
                   .HasMaxLength(300)
                   .IsRequired();

            builder.Property(a => a.BuildingNumber)
                   .HasMaxLength(150)
                   .IsRequired();

            builder.Property(a => a.ZipCode)
                   .HasMaxLength(50)
                   .IsRequired();

            builder.Property(a => a.City)
                   .HasMaxLength(300)
                   .IsRequired();

            builder.Property(a => a.Country)
                   .HasMaxLength(300)
                   .IsRequired();

            builder.HasIndex(a => a.ZipCode);
        }
    }
}
