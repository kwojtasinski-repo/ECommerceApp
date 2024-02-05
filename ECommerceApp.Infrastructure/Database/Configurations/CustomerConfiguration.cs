using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Database.Configurations
{
    internal class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.FirstName)
                   .HasMaxLength(300)
                   .IsRequired();

            builder.Property(c => c.LastName)
                   .HasMaxLength(300)
                   .IsRequired();

            builder.Property(c => c.NIP)
                   .HasMaxLength(50);

            builder.Property(c => c.CompanyName)
                   .HasMaxLength(300);

            builder.Property(c => c.IsCompany)
                   .IsRequired();

            builder.HasIndex(c => c.NIP);
        }
    }
}
