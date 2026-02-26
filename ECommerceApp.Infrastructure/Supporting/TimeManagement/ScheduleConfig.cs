using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Supporting.TimeManagement;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement
{
    internal sealed class ScheduleConfig : IScheduleConfig
    {
        public string JobName { get; init; } = default!;
        public JobType JobType { get; init; }
        public string? CronExpression { get; init; }
        public string? TimeZoneId { get; init; }
        public int MaxRetries { get; init; }
    }
}
