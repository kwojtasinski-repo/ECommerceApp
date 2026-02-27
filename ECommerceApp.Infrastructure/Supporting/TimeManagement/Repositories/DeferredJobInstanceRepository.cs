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
            _context.DeferredJobQueue.Add(instance);
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeletePendingAsync(string jobName, string entityId, CancellationToken ct = default)
        {
            var pending = await _context.DeferredJobQueue
                .Where(d => d.JobName == jobName
                         && d.EntityId == entityId
                         && d.Status == DeferredJobStatus.Pending)
                .ToListAsync(ct);

            if (pending.Count > 0)
            {
                _context.DeferredJobQueue.RemoveRange(pending);
                await _context.SaveChangesAsync(ct);
            }
        }
    }
}
