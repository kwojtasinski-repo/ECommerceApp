using ECommerceApp.Domain.Supporting.Currencies;
using ECommerceApp.Domain.Supporting.Currencies.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Supporting.Currencies.Configurations
{
    internal sealed class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
    {
        public void Configure(EntityTypeBuilder<Currency> builder)
        {
            builder.ToTable("Currencies");

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id)
                   .HasConversion(x => x.Value, v => new CurrencyId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(c => c.Code)
                   .HasConversion(x => x.Value, v => new CurrencyCode(v))
                   .HasMaxLength(3)
                   .IsRequired();

            builder.Property(c => c.Description)
                   .HasConversion(x => x.Value, v => new CurrencyDescription(v))
                   .HasMaxLength(300)
                   .IsRequired();

            builder.HasIndex(c => c.Code).IsUnique();
        }
    }
}
