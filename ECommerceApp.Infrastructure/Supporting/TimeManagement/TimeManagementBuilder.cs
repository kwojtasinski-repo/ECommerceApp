using ECommerceApp.Domain.Supporting.TimeManagement;
using System.Collections.Generic;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement
{
    public sealed class TimeManagementBuilder
    {
        internal List<ScheduleConfig> Configs { get; } = new();

        public TimeManagementBuilder AddRecurring(string jobName, string cron, int maxRetries = 3, string? timeZoneId = null)
        {
            Configs.Add(new ScheduleConfig
            {
                JobName = jobName,
                JobType = JobType.Recurring,
                CronExpression = cron,
                TimeZoneId = timeZoneId,
                MaxRetries = maxRetries
            });
            return this;
        }

        public TimeManagementBuilder AddDeferred(string jobName, int maxRetries = 3)
        {
            Configs.Add(new ScheduleConfig
            {
                JobName = jobName,
                JobType = JobType.Deferred,
                MaxRetries = maxRetries
            });
            return this;
        }
    }
}
