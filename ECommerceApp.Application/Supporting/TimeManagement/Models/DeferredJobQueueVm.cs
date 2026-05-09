using ECommerceApp.Domain.Supporting.TimeManagement;
using System;

namespace ECommerceApp.Application.Supporting.TimeManagement.Models
{
    public sealed class DeferredJobQueueVm
    {
        public int Id { get; init; }
        public string JobName { get; init; } = default!;
        public string EntityId { get; init; } = default!;
        public DateTime RunAt { get; init; }
        public DeferredJobStatus Status { get; init; }
        public int RetryCount { get; init; }
        public int MaxRetries { get; init; }
        public DateTime CreatedAt { get; init; }
        public string ErrorMessage { get; init; }
    }
}
