using ECommerceApp.Domain.Sales.Fulfillment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Sales.Fulfillment.Configurations
{
    internal sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
    {
        public void Configure(EntityTypeBuilder<Refund> builder)
        {
            builder.ToTable("Refunds");

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id)
                   .HasConversion(x => x.Value, v => new RefundId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(r => r.OrderId).IsRequired();
            builder.HasIndex(r => r.OrderId);

            builder.Property(r => r.UserId).HasMaxLength(450).IsRequired();
            builder.HasIndex(r => r.UserId);
            builder.Property(r => r.Reason).HasMaxLength(1000).IsRequired();
            builder.Property(r => r.OnWarranty).IsRequired();

            builder.Property(r => r.Status)
                   .HasConversion<string>()
                   .HasMaxLength(20)
                   .IsRequired();

            builder.Property(r => r.RequestedAt).IsRequired();
            builder.Property(r => r.ProcessedAt);

            builder.OwnsMany(r => r.Items, item =>
            {
                item.ToTable("RefundItems");
                item.WithOwner().HasForeignKey("RefundId");
                item.HasKey(i => i.Id);
                item.Property(i => i.Id).ValueGeneratedOnAdd();
                item.Property(i => i.ProductId).IsRequired();
                item.Property(i => i.Quantity).IsRequired();
            });

            builder.Navigation(r => r.Items)
                   .HasField("_items")
                   .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
