using ECommerceApp.Application.Backoffice.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    internal sealed class BackofficeJobService : IBackofficeJobService
    {
        public Task<BackofficeJobListVm> GetJobsAsync(int pageSize, int pageNo, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<BackofficeJobDetailVm?> GetJobDetailAsync(int jobId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<BackofficeJobListVm> GetJobHistoryAsync(int pageSize, int pageNo, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
