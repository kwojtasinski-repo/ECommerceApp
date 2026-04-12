using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Supporting.TimeManagement;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    internal sealed class BackofficeJobService : IBackofficeJobService
    {
        private readonly IJobManagementService _jobManagement;

        public BackofficeJobService(IJobManagementService jobManagement)
        {
            _jobManagement = jobManagement;
        }

        public async Task<BackofficeJobListVm> GetJobsAsync(int pageSize, int pageNo, CancellationToken ct = default)
        {
            var all = await _jobManagement.GetAllJobsAsync(ct);
            var items = all
                .Skip((pageNo - 1) * pageSize)
                .Take(pageSize)
                .Select((j, i) => new BackofficeJobItemVm
                {
                    Id = (pageNo - 1) * pageSize + i + 1,
                    Name = j.JobName,
                    Status = j.IsEnabled ? "Enabled" : "Disabled",
                    LastRunAt = j.LastRunAt?.ToString("O") ?? string.Empty,
                    NextRunAt = j.NextRunAt?.ToString("O") ?? string.Empty
                })
                .ToList();

            return new BackofficeJobListVm
            {
                Jobs = items,
                CurrentPage = pageNo,
                PageSize = pageSize,
                TotalCount = all.Count
            };
        }

        public async Task<BackofficeJobDetailVm?> GetJobDetailAsync(string jobName, CancellationToken ct = default)
        {
            var all = await _jobManagement.GetAllJobsAsync(ct);
            var job = all.FirstOrDefault(j => j.JobName == jobName);
            if (job is null)
                return null;

            return new BackofficeJobDetailVm
            {
                Id = 0,
                Name = job.JobName,
                Status = job.IsEnabled ? "Enabled" : "Disabled",
                CronExpression = job.Schedule ?? string.Empty,
                LastRunAt = job.LastRunAt?.ToString("O") ?? string.Empty,
                NextRunAt = job.NextRunAt?.ToString("O") ?? string.Empty
            };
        }

        public async Task<BackofficeJobListVm> GetJobHistoryAsync(int pageSize, int pageNo, CancellationToken ct = default)
        {
            var records = await _jobManagement.GetAllHistoryAsync(pageNo, pageSize, ct);
            var count = await _jobManagement.GetAllHistoryCountAsync(ct);

            var items = records
                .Select((r, i) => new BackofficeJobItemVm
                {
                    Id = (pageNo - 1) * pageSize + i + 1,
                    Name = r.JobName,
                    Status = r.Succeeded ? "Succeeded" : "Failed",
                    LastRunAt = r.CompletedAt.ToString("O"),
                    NextRunAt = string.Empty
                })
                .ToList();

            return new BackofficeJobListVm
            {
                Jobs = items,
                CurrentPage = pageNo,
                PageSize = pageSize,
                TotalCount = count
            };
        }
    }
}

