using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Database.Configurations
{
    internal class ContactDetailConfiguration : IEntityTypeConfiguration<ContactDetail>
    {
        public void Configure(EntityTypeBuilder<ContactDetail> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.ContactDetailInformation)
                   .HasMaxLength(300)
                   .IsRequired();
            
            builder.HasIndex(c => c.ContactDetailInformation);
        }
    }
}
