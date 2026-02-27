using ECommerceApp.Domain.Supporting.TimeManagement;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement.Repositories
{
    internal sealed class JobExecutionRepository : IJobExecutionRepository
    {
        private readonly TimeManagementDbContext _context;

        public JobExecutionRepository(TimeManagementDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<JobExecution>> GetPagedByJobNameAsync(
            string jobName, int page, int pageSize, CancellationToken ct = default)
        {
            return await _context.JobExecutions
                .AsNoTracking()
                .Where(e => e.JobName == jobName)
                .OrderByDescending(e => e.StartedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public async Task AddAsync(JobExecution execution, CancellationToken ct = default)
        {
            _context.JobExecutions.Add(execution);
            await _context.SaveChangesAsync(ct);
        }
    }
}
