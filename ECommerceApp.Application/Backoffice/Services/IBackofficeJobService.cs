using ECommerceApp.Application.Backoffice.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    public interface IBackofficeJobService
    {
        Task<BackofficeJobListVm> GetJobsAsync(int pageSize, int pageNo, CancellationToken ct = default);
        Task<BackofficeJobDetailVm?> GetJobDetailAsync(int jobId, CancellationToken ct = default);
        Task<BackofficeJobListVm> GetJobHistoryAsync(int pageSize, int pageNo, CancellationToken ct = default);
    }
}
