using ECommerceApp.Domain.Supporting.TimeManagement.ValueObjects;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Supporting.TimeManagement
{
    public interface IDeferredJobInstanceRepository
    {
        Task AddAsync(DeferredJobInstance instance, CancellationToken ct = default);
        Task DeletePendingAsync(string jobName, string entityId, CancellationToken ct = default);
        Task<IReadOnlyList<DeferredJobInstance>> GetAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Atomically acquires a time-limited, exclusive LEASE on one deferred job row so that exactly one
        /// poller instance runs it. Returns true when this caller now owns the row (Status becomes Running and
        /// LockExpiresAt becomes <paramref name="leaseExpiresAt"/>); false when it does not, and the caller
        /// must skip the row without treating it as an error.
        /// <para>
        /// WHY (business): a deferred job is a business action scheduled for later (expire a soft reservation,
        /// expire an unpaid payment window, apply a stock adjustment, check a guest account link, snapshot
        /// order items). Running it twice is wasteful and, for jobs with side effects (an audit entry and an
        /// availability event, a verification mail or code), visible to customers, while never running it is
        /// not acceptable. Several poller instances (more hosts on one database) share one table, and the
        /// poller's candidate query is only a snapshot, so two pollers can see the same row. The lease makes
        /// the database, not the snapshot, decide who runs it. It is a lease and not a lock held while the job
        /// runs because the job can be slow and the process can die mid-way: the right to the row must expire
        /// on its own, otherwise one crashed host would freeze the job forever.
        /// </para>
        /// <para>
        /// WHY (conditions), keywords: LEASE, DUE, ZOMBIE, ATOMIC.
        /// A row is leasable in exactly two situations, mirrored by the candidate query in
        /// DeferredJobPollerService:
        /// (1) DUE: Status == Pending and RunAt &lt;= now. Pending means nobody owns it; RunAt is the scheduled
        /// time, or the retry backoff after a failure, so a job is neither started early nor retried before
        /// its backoff elapsed.
        /// (2) ZOMBIE: Status == Running and LockExpiresAt &lt; now. Running means a poller leased it, but its
        /// lease ran out without a result being persisted, so that host is presumed dead and the row is taken
        /// over (the poller then resets it to Pending with a retry, it does not run it immediately).
        /// A Running row with a live lease is never leasable: its owner is still working on it.
        /// The check and the state change are ONE conditional statement (ATOMIC), never read-then-write:
        /// reading first re-opens the race the lease exists to close.
        /// </para>
        /// <para>
        /// NOT covered: recurring jobs (IScheduledTask) are triggered by CronSchedulerService on every host
        /// independently; there is no row to lease there. That is a separate problem.
        /// </para>
        /// <para>
        /// DOC-SYNC: if you change the conditions, the lease length, the Status transitions or this contract,
        /// you must also update (a) the candidate query in DeferredJobPollerService.PollAsync, (b)
        /// DeferredJobInstanceRepository, (c) InMemoryDeferredJobLeaseDecorator in Shared.TestInfrastructure,
        /// (d) DeferredJobPollerService, (e) the tests DeferredJobLeaseConcurrencyE2ETests and
        /// DeferredJobLeaseInMemoryDecoratorTests, and (f) this description and
        /// docs/adr/0009/deferred-job-lease-and-inmemory-test-decorator.md. A stale description is a defect.
        /// InMemory test hosts cannot run the production implementation and substitute the decorator, see that
        /// document.
        /// </para>
        /// </summary>
        Task<bool> TryAcquireLeaseAsync(DeferredJobInstanceId id, DateTime now, DateTime leaseExpiresAt, CancellationToken ct = default);
    }
}
