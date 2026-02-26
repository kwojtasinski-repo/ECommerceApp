using ECommerceApp.Domain.Supporting.TimeManagement;

namespace ECommerceApp.Application.Supporting.TimeManagement
{
    public interface IScheduleConfig
    {
        string JobName { get; }
        JobType JobType { get; }
        string? CronExpression { get; }
        string? TimeZoneId { get; }
        int MaxRetries { get; }
    }
}
