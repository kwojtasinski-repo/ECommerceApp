using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Database.Configurations
{
    internal class CouponUsedConfiguration : IEntityTypeConfiguration<CouponUsed>
    {
        public void Configure(EntityTypeBuilder<CouponUsed> builder)
        {
            builder.HasKey(c => c.Id);
        }
    }
}
