using ECommerceApp.Domain.Supporting.Currencies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Supporting.Currencies.Configurations
{
    internal sealed class CurrencyRateConfiguration : IEntityTypeConfiguration<CurrencyRate>
    {
        public void Configure(EntityTypeBuilder<CurrencyRate> builder)
        {
            builder.ToTable("CurrencyRates");

            builder.HasKey(cr => cr.Id);
            builder.Property(cr => cr.Id)
                   .HasConversion(x => x.Value, v => new CurrencyRateId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(cr => cr.CurrencyId)
                   .HasConversion(x => x.Value, v => new CurrencyId(v))
                   .IsRequired();

            builder.Property(cr => cr.Rate)
                   .IsRequired()
                   .HasPrecision(18, 4);

            builder.Property(cr => cr.CurrencyDate)
                   .IsRequired();

            builder.HasOne<Currency>()
                   .WithMany()
                   .HasForeignKey(cr => cr.CurrencyId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
