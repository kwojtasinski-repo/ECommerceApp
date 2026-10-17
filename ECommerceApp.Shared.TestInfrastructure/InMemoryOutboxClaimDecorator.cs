using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Messaging;
using ECommerceApp.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Shared.TestInfrastructure
{
    /// <summary>
    /// Test-only <see cref="IOutboxRepository"/> decorator for hosts running on the InMemory provider
    /// (<see cref="BcWebApplicationFactory"/>). The production lease is one conditional
    /// <c>ExecuteUpdateAsync</c>, which InMemory does not support, so this decorator replaces only
    /// <see cref="TryAcquireLeaseAsync"/> and delegates everything else to the real repository.
    /// Business reasoning for the lease conditions (LEASE, DUE, ZOMBIE, ATOMIC) and the DOC-SYNC list of
    /// everything to update when they change: <see cref="IOutboxRepository.TryAcquireLeaseAsync"/>.
    /// <para>
    /// InMemory never leaves the process, so a process-wide gate around read-check-write gives the same
    /// exactly-one-winner guarantee the database gives in production. This is a stand-in, not proof of the
    /// production behaviour: that is covered only by <c>OutboxClaimConcurrencyE2ETests</c> (real SQL Server).
    /// </para>
    /// <para>
    /// Background and rules for anyone adding Outbox functionality:
    /// docs/adr/0010/outbox-claim-and-inmemory-test-decorator.md
    /// </para>
    /// </summary>
    internal sealed class InMemoryOutboxClaimDecorator : IOutboxRepository
    {
        private static readonly SemaphoreSlim ClaimGate = new(1, 1);

        private readonly IOutboxRepository _inner;
        private readonly IMessagingDbContext _context;

        public InMemoryOutboxClaimDecorator(IOutboxRepository inner, IMessagingDbContext context)
        {
            _inner = inner;
            _context = context;
        }

        public async Task<bool> TryAcquireLeaseAsync(long id, DateTime now, DateTime leaseExpiresAt, CancellationToken ct = default)
        {
            await ClaimGate.WaitAsync(ct);
            try
            {
                // Same predicate as the production lease and GetDueAsync (DUE or ZOMBIE) — keep the three in sync.
                var row = await _context.Outbox
                    .FirstOrDefaultAsync(m => m.Id == id
                        && ((m.Status == OutboxStatus.Pending && m.NextAttemptAt <= now)
                            || (m.Status == OutboxStatus.Running && m.LockExpiresAt < now)), ct);
                if (row is null)
                {
                    return false;
                }

                row.MarkRunning(leaseExpiresAt);
                await _context.SaveChangesAsync(ct);

                // The poller later attaches its own copy of this row via UpdateAsync. Leaving this instance
                // tracked would be a duplicate-key identity conflict if both share a context; the production
                // claim never tracks the row at all, so mirror that.
                ((DbContext)_context).Entry(row).State = EntityState.Detached;
                return true;
            }
            finally
            {
                ClaimGate.Release();
            }
        }

        public Task<long> AddAsync(OutboxMessage message, CancellationToken ct = default)
        {
            return _inner.AddAsync(message, ct);
        }

        public Task<IReadOnlyList<OutboxMessage>> GetDueAsync(int batchSize, CancellationToken ct = default)
        {
            return _inner.GetDueAsync(batchSize, ct);
        }

        public Task<IReadOnlyList<OutboxMessage>> GetSinceAsync(DateTime sinceUtc, int batchSize, CancellationToken ct = default)
        {
            return _inner.GetSinceAsync(sinceUtc, batchSize, ct);
        }

        public Task UpdateAsync(OutboxMessage message, CancellationToken ct = default)
        {
            return _inner.UpdateAsync(message, ct);
        }

        public Task<int> DeleteDispatchedOlderThanAsync(DateTime cutoff, CancellationToken ct = default)
        {
            return _inner.DeleteDispatchedOlderThanAsync(cutoff, ct);
        }
    }
}
