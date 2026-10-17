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

        public async Task<IReadOnlyList<OutboxMessage>> GetSinceAsync(
            System.DateTime sinceUtc,
            int batchSize,
            CancellationToken ct = default)
        {
            return await _context.Outbox
                .Where(m => m.CreatedAt >= sinceUtc)
                .OrderBy(m => m.CreatedAt)
                .Take(batchSize)
                .ToListAsync(ct);
        }

        public async Task<bool> TryAcquireLeaseAsync(long id, System.DateTime now, System.DateTime leaseExpiresAt, CancellationToken ct = default)
        {
            // LEASE, ATOMIC: a single conditional UPDATE. The database serializes concurrent writers on the
            // row, so the first one flips it to Running with a live lease and every other one matches 0
            // rows. Covers a DUE Pending row and a ZOMBIE (Running with an expired lease), with the same
            // predicate GetDueAsync uses. The business reasoning for these exact conditions is on
            // IOutboxRepository.TryAcquireLeaseAsync; if you change this predicate, update that description
            // (DOC-SYNC list there) as well.
            //
            // ExecuteUpdateAsync is relational-only: this method throws on the InMemory provider. Test hosts
            // built on BcWebApplicationFactory therefore decorate IOutboxRepository with
            // InMemoryOutboxClaimDecorator. Rationale and the rules for anyone adding Outbox functionality:
            // docs/adr/0010/outbox-claim-and-inmemory-test-decorator.md
            var claimed = await _context.Outbox
                .Where(m => m.Id == id
                    && ((m.Status == OutboxStatus.Pending && m.NextAttemptAt <= now)
                        || (m.Status == OutboxStatus.Running && m.LockExpiresAt < now)))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(m => m.Status, OutboxStatus.Running)
                    .SetProperty(m => m.LockExpiresAt, leaseExpiresAt), ct);
            return claimed == 1;
        }

        public async Task UpdateAsync(OutboxMessage message, CancellationToken ct = default)
        {
            _context.Outbox.Update(message);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<int> DeleteDispatchedOlderThanAsync(System.DateTime cutoff, CancellationToken ct = default)
        {
            var messages = await _context.Outbox
                .Where(m => m.Status == OutboxStatus.Dispatched
                         && m.DispatchedAt.HasValue
                         && m.DispatchedAt.Value < cutoff)
                .ToListAsync(ct);

            _context.Outbox.RemoveRange(messages);
            await _context.SaveChangesAsync(ct);
            return messages.Count;
        }
    }
}
