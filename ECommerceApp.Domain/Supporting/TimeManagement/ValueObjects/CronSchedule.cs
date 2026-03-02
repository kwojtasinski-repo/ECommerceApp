using Cronos;
using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Supporting.TimeManagement.ValueObjects
{
    public sealed record CronSchedule
    {
        public string Value { get; }

        public CronSchedule(string value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                throw new DomainException("Cron expression is required.");
            var parts = trimmed.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5)
                throw new DomainException("Cron expression must have exactly 5 parts (minutes granularity only, no seconds field).");
            try
            {
                CronExpression.Parse(trimmed);
            }
            catch (CronFormatException ex)
            {
                throw new DomainException($"Invalid cron expression: {ex.Message}");
            }
            Value = trimmed;
        }

        public override string ToString() => Value;
    }
}
