using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Database.Configurations
{
    internal class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
    {
        public void Configure(EntityTypeBuilder<Currency> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Code)
                   .HasMaxLength(3)
                   .IsRequired();

            builder.Property(c => c.Description)
                   .HasMaxLength(300)
                   .IsRequired();

            builder.HasIndex(c => c.Code).IsUnique();
        }
    }
}
