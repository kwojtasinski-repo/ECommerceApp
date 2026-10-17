using ECommerceApp.Domain.Supporting.TimeManagement;
using ECommerceApp.Domain.Supporting.TimeManagement.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement.Repositories
{
    internal sealed class DeferredJobInstanceRepository : IDeferredJobInstanceRepository
    {
        private readonly ITimeManagementDbContext _context;

        public DeferredJobInstanceRepository(ITimeManagementDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(DeferredJobInstance instance, CancellationToken ct = default)
        {
            _context.DeferredJobQueue.Add(instance);
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeletePendingAsync(string jobName, string entityId, CancellationToken ct = default)
        {
            var pending = await _context.DeferredJobQueue
                .Where(d => d.JobName == new JobName(jobName)
                         && d.EntityId == new EntityId(entityId)
                         && d.Status == DeferredJobStatus.Pending)
                .ToListAsync(ct);

            if (pending.Count > 0)
            {
                _context.DeferredJobQueue.RemoveRange(pending);
                await _context.SaveChangesAsync(ct);
            }
        }

        public async Task<bool> TryAcquireLeaseAsync(DeferredJobInstanceId id, System.DateTime now, System.DateTime leaseExpiresAt, CancellationToken ct = default)
        {
            // LEASE, ATOMIC: a single conditional UPDATE. The database serializes concurrent writers on the
            // row, so the first one flips it to Running with a live lease and every other one matches 0
            // rows. Covers a DUE Pending row and a ZOMBIE (Running with an expired lease), with the same
            // predicate the candidate query in DeferredJobPollerService.PollAsync uses. The business
            // reasoning for these exact conditions is on IDeferredJobInstanceRepository.TryAcquireLeaseAsync;
            // if you change this predicate, update that description (DOC-SYNC list there) as well.
            //
            // The Id is compared as a whole value object, not through .Value: .Value on a HasConversion
            // property does not translate to SQL Server (InMemory hides that).
            //
            // ExecuteUpdateAsync is relational-only: this method throws on the InMemory provider. Test hosts
            // built on BcWebApplicationFactory therefore decorate IDeferredJobInstanceRepository with
            // InMemoryDeferredJobLeaseDecorator. Rationale and the rules for anyone adding deferred job
            // functionality: docs/adr/0009/deferred-job-lease-and-inmemory-test-decorator.md
            //
            // SQL (EF Core, SQL Server) — captured from this method with command interceptors, not hand-written:
            //   UPDATE [d] SET [d].[Status] = @p, [d].[LockExpiresAt] = @p4
            //   FROM [time_management].[DeferredJobQueue] AS [d]
            //   WHERE [d].[Id] = @id
            //     AND (([d].[Status] = CAST(0 AS tinyint) AND [d].[RunAt] <= @now)
            //       OR ([d].[Status] = CAST(1 AS tinyint) AND [d].[LockExpiresAt] < @now2))
            // Why: DUE (Pending, RunAt reached) or ZOMBIE (Running, lease expired); one winner per row.
            var leased = await _context.DeferredJobQueue
                .Where(d => d.Id == id
                    && ((d.Status == DeferredJobStatus.Pending && d.RunAt <= now)
                        || (d.Status == DeferredJobStatus.Running && d.LockExpiresAt < now)))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(d => d.Status, DeferredJobStatus.Running)
                    .SetProperty(d => d.LockExpiresAt, leaseExpiresAt), ct);
            return leased == 1;
        }

        public async Task<IReadOnlyList<DeferredJobInstance>> GetAllAsync(CancellationToken ct = default)
            => await _context.DeferredJobQueue
                .AsNoTracking()
                .OrderBy(d => d.RunAt)
                .ToListAsync(ct);
    }
}
