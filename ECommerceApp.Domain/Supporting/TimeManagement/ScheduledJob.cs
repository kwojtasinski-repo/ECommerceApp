using ECommerceApp.Domain.Supporting.TimeManagement.ValueObjects;
using System;
using System.Security.Cryptography;
using System.Text;

namespace ECommerceApp.Domain.Supporting.TimeManagement
{
    public class ScheduledJob
    {
        public ScheduledJobId Id { get; private set; } = new ScheduledJobId(0);
        public JobName Name { get; private set; } = default!;
        public JobType JobType { get; private set; }
        public string? CronExpression { get; private set; }
        public string? TimeZoneId { get; private set; }
        public bool IsEnabled { get; private set; }
        public int MaxRetries { get; private set; }
        public DateTime? LastRunAt { get; private set; }
        public DateTime? NextRunAt { get; private set; }
        public string? ConfigHash { get; private set; }

        private ScheduledJob() { }

        public static ScheduledJob Create(string name, JobType jobType, string? cronExpression, string? timeZoneId, int maxRetries)
        {
            return new ScheduledJob
            {
                Name = new JobName(name),
                JobType = jobType,
                CronExpression = cronExpression?.Trim(),
                TimeZoneId = timeZoneId?.Trim(),
                IsEnabled = true,
                MaxRetries = maxRetries,
                ConfigHash = ComputeHash(cronExpression, timeZoneId, maxRetries)
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

        public bool SyncConfig(string? cronExpression, string? timeZoneId, int maxRetries)
        {
            var newHash = ComputeHash(cronExpression, timeZoneId, maxRetries);
            if (newHash == ConfigHash)
                return false;

            CronExpression = cronExpression?.Trim();
            TimeZoneId = timeZoneId?.Trim();
            MaxRetries = maxRetries;
            ConfigHash = newHash;
            return true;
        }

        private static string ComputeHash(string? cronExpression, string? timeZoneId, int maxRetries)
        {
            var input = $"{cronExpression}|{timeZoneId}|{maxRetries}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
