using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Supporting.TimeManagement
{
    public interface IJobExecutionRepository
    {
        Task<IReadOnlyList<JobExecution>> GetPagedByJobNameAsync(string jobName, int page, int pageSize, CancellationToken ct = default);
        Task AddAsync(JobExecution execution, CancellationToken ct = default);
    }
}
