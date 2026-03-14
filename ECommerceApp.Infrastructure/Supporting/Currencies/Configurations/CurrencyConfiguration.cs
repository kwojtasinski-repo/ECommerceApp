using ECommerceApp.Domain.Supporting.Currencies;
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

            builder.OwnsOne(c => c.Code, b =>
            {
                b.Property(x => x.Value)
                 .HasColumnName("Code")
                 .HasMaxLength(3)
                 .IsRequired();
                b.HasIndex(x => x.Value).IsUnique().HasDatabaseName("IX_Currencies_Code");
            });
            builder.Navigation(c => c.Code).IsRequired();

            builder.OwnsOne(c => c.Description, b =>
            {
                b.Property(x => x.Value)
                 .HasColumnName("Description")
                 .HasMaxLength(300)
                 .IsRequired();
            });
            builder.Navigation(c => c.Description).IsRequired();
        }
    }
}
