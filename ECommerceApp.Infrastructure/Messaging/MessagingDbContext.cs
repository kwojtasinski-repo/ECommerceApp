using ECommerceApp.Domain.Messaging;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Messaging
{
    internal sealed class MessagingDbContext : DbContext, IMessagingDbContext
    {
        public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
        public DbSet<ProcessedMessage> Inbox => Set<ProcessedMessage>();

        public MessagingDbContext(DbContextOptions<MessagingDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.HasDefaultSchema(MessagingConstants.SchemaName);
            builder.ApplyConfigurationsFromAssembly(
                GetType().Assembly,
                t => t.Namespace != null && t.Namespace.StartsWith("ECommerceApp.Infrastructure.Messaging.Configurations"));
            builder.UseUtcDateTimes();
        }
    }
}
