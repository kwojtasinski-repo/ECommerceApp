using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Supporting.TimeManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement
{
    internal sealed class DeferredJobScheduler : IDeferredJobScheduler
    {
        private readonly IScheduledJobRepository _scheduledJobRepo;
        private readonly IDeferredJobInstanceRepository _instanceRepo;
        private readonly IEnumerable<IScheduleConfig> _configs;

        public DeferredJobScheduler(
            IScheduledJobRepository scheduledJobRepo,
            IDeferredJobInstanceRepository instanceRepo,
            IEnumerable<IScheduleConfig> configs)
        {
            _scheduledJobRepo = scheduledJobRepo;
            _instanceRepo = instanceRepo;
            _configs = configs;
        }

        public async Task ScheduleAsync(string jobName, string entityId, DateTime runAt, CancellationToken ct = default)
        {
            var scheduledJob = await _scheduledJobRepo.GetByNameAsync(jobName, ct);
            if (scheduledJob == null)
            {
                var config = _configs.FirstOrDefault(c => c.JobName == jobName)
                    ?? throw new BusinessException($"No configuration registered for job '{jobName}'.");
                scheduledJob = ScheduledJob.Create(jobName, config.JobType, config.CronExpression, config.TimeZoneId, config.MaxRetries);
                await _scheduledJobRepo.AddAsync(scheduledJob, ct);
            }

            var instance = DeferredJobInstance.Schedule(scheduledJob.Id, entityId, runAt);
            await _instanceRepo.AddAsync(instance, ct);
        }

        public async Task CancelAsync(string jobName, string entityId, CancellationToken ct = default)
        {
            var scheduledJob = await _scheduledJobRepo.GetByNameAsync(jobName, ct);
            if (scheduledJob == null)
                return;

            await _instanceRepo.CancelPendingAsync(scheduledJob.Id, entityId, ct);
        }
    }
}
