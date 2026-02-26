using ECommerceApp.Application.Supporting.TimeManagement.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.TimeManagement
{
    public interface IJobManagementService
    {
        Task<IReadOnlyList<JobStatusSummary>> GetAllJobsAsync(CancellationToken ct = default);
        Task<IReadOnlyList<JobExecutionRecord>> GetHistoryAsync(string jobName, int page, int pageSize, CancellationToken ct = default);
        Task EnableAsync(string jobName, CancellationToken ct = default);
        Task DisableAsync(string jobName, CancellationToken ct = default);
    }
}
