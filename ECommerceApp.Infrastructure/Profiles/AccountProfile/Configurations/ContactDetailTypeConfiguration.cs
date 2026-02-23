using ECommerceApp.Domain.Profiles.AccountProfile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Profiles.AccountProfile.Configurations
{
    internal class ContactDetailTypeConfiguration : IEntityTypeConfiguration<ContactDetailType>
    {
        public void Configure(EntityTypeBuilder<ContactDetailType> builder)
        {
            builder.ToTable("ContactDetailTypes", AccountProfileConstants.Schema);

            builder.HasKey(t => t.Id);

            builder.Property(t => t.Name)
                   .HasMaxLength(150)
                   .IsRequired();

            builder.HasIndex(t => t.Name)
                   .IsUnique();
        }
    }
}
