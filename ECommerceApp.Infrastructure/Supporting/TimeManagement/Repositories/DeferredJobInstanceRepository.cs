using ECommerceApp.Domain.Supporting.TimeManagement;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement.Repositories
{
    internal sealed class DeferredJobInstanceRepository : IDeferredJobInstanceRepository
    {
        private readonly TimeManagementDbContext _context;

        public DeferredJobInstanceRepository(TimeManagementDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(DeferredJobInstance instance, CancellationToken ct = default)
        {
            _context.DeferredJobInstances.Add(instance);
            await _context.SaveChangesAsync(ct);
        }

        public async Task CancelPendingAsync(ScheduledJobId scheduledJobId, string entityId, CancellationToken ct = default)
        {
            var pending = await _context.DeferredJobInstances
                .Where(d => d.ScheduledJobId == scheduledJobId
                         && d.EntityId == entityId
                         && d.Status == DeferredJobStatus.Pending)
                .ToListAsync(ct);

            foreach (var instance in pending)
                instance.Cancel();

            if (pending.Count > 0)
                await _context.SaveChangesAsync(ct);
        }
    }
}
