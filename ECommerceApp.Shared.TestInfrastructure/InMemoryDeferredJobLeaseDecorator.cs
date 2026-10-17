using ECommerceApp.Domain.Supporting.TimeManagement;
using ECommerceApp.Domain.Supporting.TimeManagement.ValueObjects;
using ECommerceApp.Infrastructure.Supporting.TimeManagement;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Shared.TestInfrastructure
{
    /// <summary>
    /// Test-only <see cref="IDeferredJobInstanceRepository"/> decorator for hosts running on the InMemory
    /// provider (<see cref="BcWebApplicationFactory"/>). The production lease is one conditional
    /// <c>ExecuteUpdateAsync</c>, which InMemory does not support, so this decorator replaces only
    /// <see cref="TryAcquireLeaseAsync"/> and delegates everything else to the real repository.
    /// Business reasoning for the lease conditions (LEASE, DUE, ZOMBIE, ATOMIC) and the DOC-SYNC list of
    /// everything to update when they change: <see cref="IDeferredJobInstanceRepository.TryAcquireLeaseAsync"/>.
    /// <para>
    /// InMemory never leaves the process, so a process-wide gate around read-check-write gives the same
    /// exactly-one-winner guarantee the database gives in production. This is a stand-in, not proof of the
    /// production behaviour: that is covered only by <c>DeferredJobLeaseConcurrencyE2ETests</c> (real SQL Server).
    /// </para>
    /// <para>
    /// Background and rules for anyone adding deferred job functionality:
    /// docs/adr/0009/deferred-job-lease-and-inmemory-test-decorator.md
    /// </para>
    /// </summary>
    internal sealed class InMemoryDeferredJobLeaseDecorator : IDeferredJobInstanceRepository
    {
        private static readonly SemaphoreSlim LeaseGate = new(1, 1);

        private readonly IDeferredJobInstanceRepository _inner;
        private readonly ITimeManagementDbContext _context;

        public InMemoryDeferredJobLeaseDecorator(IDeferredJobInstanceRepository inner, ITimeManagementDbContext context)
        {
            _inner = inner;
            _context = context;
        }

        public async Task<bool> TryAcquireLeaseAsync(DeferredJobInstanceId id, DateTime now, DateTime leaseExpiresAt, CancellationToken ct = default)
        {
            await LeaseGate.WaitAsync(ct);
            try
            {
                // Same predicate as the production lease and the poller's candidate query (DUE or ZOMBIE) —
                // keep the three in sync.
                var row = await _context.DeferredJobQueue
                    .FirstOrDefaultAsync(d => d.Id == id
                        && ((d.Status == DeferredJobStatus.Pending && d.RunAt <= now)
                            || (d.Status == DeferredJobStatus.Running && d.LockExpiresAt < now)), ct);
                if (row is null)
                {
                    return false;
                }

                row.MarkRunning(leaseExpiresAt);
                await _context.SaveChangesAsync(ct);

                // The production lease never tracks the row; mirror that so a later load of the same row in
                // the same scope (zombie recovery) reads fresh state instead of this instance.
                ((DbContext)_context).Entry(row).State = EntityState.Detached;
                return true;
            }
            finally
            {
                LeaseGate.Release();
            }
        }

        public Task AddAsync(DeferredJobInstance instance, CancellationToken ct = default)
        {
            return _inner.AddAsync(instance, ct);
        }

        public Task DeletePendingAsync(string jobName, string entityId, CancellationToken ct = default)
        {
            return _inner.DeletePendingAsync(jobName, entityId, ct);
        }

        public Task<IReadOnlyList<DeferredJobInstance>> GetAllAsync(CancellationToken ct = default)
        {
            return _inner.GetAllAsync(ct);
        }
    }
}
