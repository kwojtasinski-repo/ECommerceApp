using System;

namespace ECommerceApp.Domain.Supporting.TimeManagement.Events
{
    public record JobExecutionFailed(
        int ScheduledJobId,
        string ExecutionId,
        DateTime FailedAt,
        string Error);
}
