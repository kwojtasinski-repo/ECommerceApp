using System;

namespace ECommerceApp.Application.Supporting.TimeManagement.Models
{
    public sealed class JobStatusSummary
    {
        public string JobName { get; init; } = default!;
        public string? Schedule { get; init; }
        public bool IsEnabled { get; init; }
        public DateTime? LastRunAt { get; init; }
        public DateTime? NextRunAt { get; init; }
        public bool? LastSucceeded { get; init; }
        public string? LastMessage { get; init; }
        public string? LastExecutionId { get; init; }
        public bool NeverRun { get; init; }
    }
}
