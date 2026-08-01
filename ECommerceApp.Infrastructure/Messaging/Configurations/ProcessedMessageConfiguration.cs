using ECommerceApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Messaging.Configurations
{
    internal sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
    {
        public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
        {
            builder.ToTable("Inbox");
            builder.HasKey(x => new { x.MessageId, x.HandlerType });

            builder.Property(x => x.MessageId)
                .IsRequired();
            builder.Property(x => x.HandlerType)
                .HasMaxLength(500)
                .IsRequired();
            builder.Property(x => x.ProcessedAt)
                .IsRequired();
        }
    }
}