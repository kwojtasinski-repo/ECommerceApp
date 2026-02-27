using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Supporting.TimeManagement;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement
{
    internal sealed class DeferredJobScheduler : IDeferredJobScheduler
    {
        private readonly IDeferredJobInstanceRepository _instanceRepo;

        public DeferredJobScheduler(IDeferredJobInstanceRepository instanceRepo)
        {
            _instanceRepo = instanceRepo;
        }

        public async Task ScheduleAsync(string jobName, string entityId, DateTime runAt, CancellationToken ct = default)
        {
            var instance = DeferredJobInstance.Schedule(jobName, entityId, runAt);
            await _instanceRepo.AddAsync(instance, ct);
        }

        public async Task CancelAsync(string jobName, string entityId, CancellationToken ct = default)
        {
            await _instanceRepo.DeletePendingAsync(jobName, entityId, ct);
        }
    }
}
