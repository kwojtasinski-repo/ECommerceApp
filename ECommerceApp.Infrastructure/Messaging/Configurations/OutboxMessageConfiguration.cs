using ECommerceApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.Messaging.Configurations
{
    internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        {
            builder.ToTable("Outbox");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id)
                   .ValueGeneratedOnAdd();

            builder.Property(x => x.MessageTypeKey)
                   .HasMaxLength(200)
                   .IsRequired();

            builder.Property(x => x.Payload)
                   .HasColumnType("nvarchar(max)")
                   .IsRequired();

            builder.Property(x => x.CreatedAt)
                   .IsRequired();

            builder.Property(x => x.Status)
                   .IsRequired();

            builder.Property(x => x.DispatchedAt);

            builder.Property(x => x.LockExpiresAt);

            builder.Property(x => x.NextAttemptAt)
                   .IsRequired();

            builder.Property(x => x.RetryCount)
                   .IsRequired();

            builder.Property(x => x.MaxRetries)
                   .IsRequired();

            builder.Property(x => x.ErrorMessage);

            // Supports the poller's due-row query: WHERE Status = Pending/Running ORDER BY ... (Phase 2).
            builder.HasIndex(x => new { x.Status, x.CreatedAt });
        }
    }
}
