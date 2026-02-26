using System;

namespace ECommerceApp.Domain.Supporting.TimeManagement.Events
{
    public record JobExecutionCompleted(
        int ScheduledJobId,
        string ExecutionId,
        DateTime CompletedAt,
        string? Message);
}
