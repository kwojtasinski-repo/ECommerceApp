using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Messaging.Repositories
{
    internal sealed class OutboxRepository : IOutboxRepository
    {
        private readonly IMessagingDbContext _context;

        public OutboxRepository(IMessagingDbContext context)
        {
            _context = context;
        }

        public async Task<long> AddAsync(OutboxMessage message, CancellationToken ct = default)
        {
            _context.Outbox.Add(message);
            await _context.SaveChangesAsync(ct);
            return message.Id;
        }

        public async Task<IReadOnlyList<OutboxMessage>> GetDueAsync(int batchSize, CancellationToken ct = default)
        {
            var now = System.DateTime.UtcNow;
            return await _context.Outbox
                .Where(m => (m.Status == OutboxStatus.Pending && m.NextAttemptAt <= now)
                         || (m.Status == OutboxStatus.Running && m.LockExpiresAt < now))
                .OrderBy(m => m.NextAttemptAt)
                .Take(batchSize)
                .ToListAsync(ct);
        }

        public async Task UpdateAsync(OutboxMessage message, CancellationToken ct = default)
        {
            _context.Outbox.Update(message);
            await _context.SaveChangesAsync(ct);
        }
    }
}
