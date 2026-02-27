using ECommerceApp.Domain.Supporting.TimeManagement.ValueObjects;
using System;

namespace ECommerceApp.Domain.Supporting.TimeManagement
{
    public class ScheduledJob
    {
        public ScheduledJobId Id { get; private set; } = new ScheduledJobId(0);
        public JobName Name { get; private set; } = default!;
        public string Schedule { get; private set; } = default!;
        public string? TimeZoneId { get; private set; }
        public bool IsEnabled { get; private set; }
        public int MaxRetries { get; private set; }
        public DateTime? LastRunAt { get; private set; }
        public DateTime? NextRunAt { get; private set; }

        private ScheduledJob() { }

        public static ScheduledJob Create(string name, string schedule, string? timeZoneId, int maxRetries)
        {
            return new ScheduledJob
            {
                Name = new JobName(name),
                Schedule = schedule.Trim(),
                TimeZoneId = timeZoneId?.Trim(),
                IsEnabled = true,
                MaxRetries = maxRetries
            };
        }

        public void Enable()
        {
            IsEnabled = true;
        }

        public void Disable()
        {
            IsEnabled = false;
        }

        public void RecordRun(DateTime completedAt, DateTime? nextRunAt)
        {
            LastRunAt = completedAt;
            NextRunAt = nextRunAt;
        }
    }
}
