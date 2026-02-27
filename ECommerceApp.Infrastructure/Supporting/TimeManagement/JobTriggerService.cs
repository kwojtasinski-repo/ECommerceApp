using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using ECommerceApp.Domain.Supporting.TimeManagement;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement
{
    internal sealed class JobTriggerService : IJobTrigger
    {
        private readonly JobTriggerChannel _channel;
        private readonly IScheduledJobRepository _scheduledJobRepo;

        public JobTriggerService(
            JobTriggerChannel channel,
            IScheduledJobRepository scheduledJobRepo)
        {
            _channel = channel;
            _scheduledJobRepo = scheduledJobRepo;
        }

        public async Task TriggerAsync(string jobName, CancellationToken ct = default)
        {
            var job = await _scheduledJobRepo.GetByNameAsync(jobName, ct)
                ?? throw new BusinessException($"Job '{jobName}' is not registered.");

            await _channel.WriteAsync(new JobTriggerRequest
            {
                JobName = job.Name.Value,
                Source = JobTriggerSource.Manual
            }, ct);
        }
    }
}
