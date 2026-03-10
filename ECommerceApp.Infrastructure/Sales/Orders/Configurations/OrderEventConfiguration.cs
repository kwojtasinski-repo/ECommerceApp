using ECommerceApp.Domain.Sales.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Sales.Orders.Configurations
{
    internal sealed class OrderEventConfiguration : IEntityTypeConfiguration<OrderEvent>
    {
        public void Configure(EntityTypeBuilder<OrderEvent> builder)
        {
            builder.ToTable("OrderEvents");

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id)
                   .HasConversion(x => x.Value, v => new OrderEventId(v))
                   .ValueGeneratedOnAdd();

            builder.Property(e => e.OrderId)
                   .HasConversion(id => id.Value, v => new OrderId(v))
                   .IsRequired();

            builder.Property(e => e.EventType)
                   .HasConversion<string>()
                   .HasMaxLength(50)
                   .IsRequired();

            builder.Property(e => e.Payload);

            builder.Property(e => e.OccurredAt).IsRequired();

            builder.HasIndex(e => new { e.OrderId, e.OccurredAt });
        }
    }
}
