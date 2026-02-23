using ECommerceApp.Domain.Profiles.AccountProfile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Profiles.AccountProfile.Configurations
{
    internal class ContactDetailConfiguration : IEntityTypeConfiguration<ContactDetail>
    {
        public void Configure(EntityTypeBuilder<ContactDetail> builder)
        {
            builder.ToTable("ContactDetails", AccountProfileConstants.Schema);

            builder.HasKey(c => c.Id);

            builder.Property(c => c.Information)
                   .HasMaxLength(300)
                   .IsRequired();

            builder.Property(c => c.ContactDetailTypeId)
                   .IsRequired();

            builder.Property(c => c.AccountProfileId)
                   .IsRequired();

            builder.HasIndex(c => c.Information);
            builder.HasIndex(c => c.AccountProfileId);
        }
    }
}
