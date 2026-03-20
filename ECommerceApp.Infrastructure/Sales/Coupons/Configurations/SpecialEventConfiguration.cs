using ECommerceApp.Domain.Sales.Coupons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Sales.Coupons.Configurations
{
    internal sealed class SpecialEventConfiguration : IEntityTypeConfiguration<SpecialEvent>
    {
        public void Configure(EntityTypeBuilder<SpecialEvent> builder)
        {
            builder.ToTable("SpecialEvents");

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id)
                   .HasConversion(x => x.Value, v => new SpecialEventId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(e => e.Code)
                   .HasMaxLength(50)
                   .IsRequired();
            builder.HasIndex(e => e.Code).IsUnique();

            builder.Property(e => e.Name)
                   .HasMaxLength(200)
                   .IsRequired();

            builder.Property(e => e.StartsAt).IsRequired();
            builder.Property(e => e.EndsAt).IsRequired();
            builder.Property(e => e.IsActive).IsRequired();
        }
    }
}
