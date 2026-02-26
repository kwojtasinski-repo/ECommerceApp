using System;

namespace ECommerceApp.Application.Supporting.TimeManagement.Models
{
    public sealed class JobExecutionRecord
    {
        public string JobName { get; init; } = default!;
        public string ExecutionId { get; init; } = default!;
        public DateTime StartedAt { get; init; }
        public DateTime CompletedAt { get; init; }
        public bool Succeeded { get; init; }
        public string? Message { get; init; }
        public JobTriggerSource Source { get; init; }
    }
}
