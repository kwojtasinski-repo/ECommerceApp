using ECommerceApp.Domain.Supporting.TimeManagement;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement.Repositories
{
    internal sealed class ScheduledJobRepository : IScheduledJobRepository
    {
        private readonly TimeManagementDbContext _context;

        public ScheduledJobRepository(TimeManagementDbContext context)
        {
            _context = context;
        }

        public async Task<ScheduledJob?> GetByNameAsync(string name, CancellationToken ct = default)
            => await _context.ScheduledJobs
                .FirstOrDefaultAsync(j => j.Name.Value == name, ct);

        public async Task<IReadOnlyList<ScheduledJob>> GetAllAsync(CancellationToken ct = default)
            => await _context.ScheduledJobs
                .AsNoTracking()
                .ToListAsync(ct);

        public async Task AddAsync(ScheduledJob job, CancellationToken ct = default)
        {
            _context.ScheduledJobs.Add(job);
            await _context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(ScheduledJob job, CancellationToken ct = default)
        {
            _context.ScheduledJobs.Update(job);
            await _context.SaveChangesAsync(ct);
        }
    }
}
