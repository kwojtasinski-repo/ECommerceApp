using ECommerceApp.Domain.Messaging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Messaging
{
    public interface IOutboxRepository
    {
        Task<long> AddAsync(OutboxMessage message, CancellationToken ct = default);
        Task<IReadOnlyList<OutboxMessage>> GetDueAsync(int batchSize, CancellationToken ct = default);
        Task<IReadOnlyList<OutboxMessage>> GetSinceAsync(DateTime sinceUtc, int batchSize, CancellationToken ct = default);
        /// <summary>
        /// Atomically acquires a time-limited, exclusive LEASE on one Outbox row so that exactly one poller
        /// dispatches it. Returns true when this caller now owns the row (Status becomes Running and
        /// LockExpiresAt becomes <paramref name="leaseExpiresAt"/>); false when it does not, and the caller
        /// must skip the row without treating it as an error.
        /// <para>
        /// WHY (business): an Outbox row is a business event that must reach its handlers (order placed,
        /// stock changed, payment confirmed...). Delivery is at-least-once, so a duplicate is tolerated but
        /// is wasteful and for non-idempotent handlers harmful, while a lost event is not tolerated at all.
        /// Several poller instances (more hosts on one database, or a poller racing a test) share one table,
        /// and GetDueAsync is only a snapshot, so two pollers can see the same row. The lease makes the
        /// database, not the snapshot, decide who dispatches. It is a lease and not a lock held during
        /// dispatch because dispatch can be slow and the process can die mid-way: the right to the row must
        /// expire on its own, otherwise one crashed poller would freeze the event forever.
        /// </para>
        /// <para>
        /// WHY (conditions), keywords: LEASE, DUE, ZOMBIE, ATOMIC.
        /// A row is leasable in exactly two situations, mirrored by GetDueAsync:
        /// (1) DUE: Status == Pending and NextAttemptAt &lt;= now. Pending means nobody owns it; NextAttemptAt
        /// is the retry backoff, so a row that failed recently is not hammered before its backoff elapsed.
        /// (2) ZOMBIE: Status == Running and LockExpiresAt &lt; now. Running means a poller leased it, but its
        /// lease ran out without MarkDispatched or Fail, so that poller is presumed dead and the row is taken
        /// over. This is the crash-recovery path and the reason redelivery can happen (at-least-once).
        /// A Running row with a live lease is never leasable: its owner is still working on it.
        /// The check and the state change are ONE conditional statement (ATOMIC), never read-then-write:
        /// reading first re-opens the race the lease exists to close.
        /// </para>
        /// <para>
        /// DOC-SYNC: if you change the conditions, the lease length, the Status transitions or this
        /// contract, you must also update (a) GetDueAsync, (b) OutboxRepository, (c)
        /// InMemoryOutboxClaimDecorator in Shared.TestInfrastructure, (d) OutboxPollerService, (e) the tests
        /// OutboxClaimConcurrencyE2ETests and OutboxClaimInMemoryDecoratorTests, and (f) this description and
        /// docs/adr/0010/outbox-claim-and-inmemory-test-decorator.md. A stale description is a defect.
        /// InMemory test hosts cannot run the production implementation and substitute the decorator, see
        /// that document.
        /// </para>
        /// </summary>
        Task<bool> TryAcquireLeaseAsync(long id, DateTime now, DateTime leaseExpiresAt, CancellationToken ct = default);
        Task UpdateAsync(OutboxMessage message, CancellationToken ct = default);
        Task<int> DeleteDispatchedOlderThanAsync(System.DateTime cutoff, CancellationToken ct = default);
    }
}
