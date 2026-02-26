using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.TimeManagement
{
    public interface IDeferredJobScheduler
    {
        Task ScheduleAsync(string jobName, string entityId, DateTime runAt, CancellationToken ct = default);
        Task CancelAsync(string jobName, string entityId, CancellationToken ct = default);
    }
}
