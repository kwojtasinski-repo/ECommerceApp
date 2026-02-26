using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement
{
    internal sealed class JobTriggerService : IJobTrigger
    {
        private readonly JobTriggerChannel _channel;
        private readonly System.Collections.Generic.IEnumerable<IScheduleConfig> _configs;

        public JobTriggerService(
            JobTriggerChannel channel,
            System.Collections.Generic.IEnumerable<IScheduleConfig> configs)
        {
            _channel = channel;
            _configs = configs;
        }

        public async Task TriggerAsync(string jobName, CancellationToken ct = default)
        {
            var found = false;
            foreach (var config in _configs)
            {
                if (config.JobName == jobName) { found = true; break; }
            }

            if (!found)
                throw new BusinessException($"Job '{jobName}' is not registered.");

            await _channel.WriteAsync(new JobTriggerRequest
            {
                JobName = jobName,
                Source = JobTriggerSource.Manual
            }, ct);
        }
    }
}
