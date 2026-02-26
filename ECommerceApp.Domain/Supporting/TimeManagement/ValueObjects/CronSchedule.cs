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
