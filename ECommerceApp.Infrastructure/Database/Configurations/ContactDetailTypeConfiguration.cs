using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Database.Configurations
{
    internal class ContactDetailTypeConfiguration : IEntityTypeConfiguration<ContactDetailType>
    {
        public void Configure(EntityTypeBuilder<ContactDetailType> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(150);

            builder.HasIndex(c => c.Name);
        }
    }
}
