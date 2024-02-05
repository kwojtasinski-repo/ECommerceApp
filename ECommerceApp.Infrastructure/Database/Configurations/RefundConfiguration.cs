using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Database.Configurations
{
    internal class RefundConfiguration : IEntityTypeConfiguration<Refund>
    {
        public void Configure(EntityTypeBuilder<Refund> builder)
        {
            builder.HasKey(r => r.Id);

            builder.Property(r => r.Reason).HasMaxLength(300)
                .IsRequired();

            builder.Property(r => r.Accepted).IsRequired();

            builder.Property(r => r.RefundDate).IsRequired();

            builder.Property(r => r.OnWarranty).IsRequired();

            builder
                .HasOne(r => r.Order)
                .WithOne(o => o.Refund)
                .HasForeignKey<Order>(o => o.RefundId);
        }
    }
}
