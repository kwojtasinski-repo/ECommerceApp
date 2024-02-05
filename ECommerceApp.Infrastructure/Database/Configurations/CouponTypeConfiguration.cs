using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Database.Configurations
{
    internal class CouponTypeConfiguration : IEntityTypeConfiguration<CouponType>
    {
        public void Configure(EntityTypeBuilder<CouponType> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Type)
                   .HasMaxLength(300)
                   .IsRequired();
        }
    }
}
