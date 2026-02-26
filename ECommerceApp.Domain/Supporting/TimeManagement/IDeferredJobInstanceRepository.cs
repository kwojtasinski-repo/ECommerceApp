using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Supporting.TimeManagement
{
    public interface IDeferredJobInstanceRepository
    {
        Task AddAsync(DeferredJobInstance instance, CancellationToken ct = default);
        Task CancelPendingAsync(ScheduledJobId scheduledJobId, string entityId, CancellationToken ct = default);
    }
}
