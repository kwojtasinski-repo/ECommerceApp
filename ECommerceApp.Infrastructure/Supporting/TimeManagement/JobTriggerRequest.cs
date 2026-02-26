using ECommerceApp.Application.Supporting.TimeManagement.Models;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement
{
    internal sealed class JobTriggerRequest
    {
        public string JobName { get; init; } = default!;
        public string? EntityId { get; init; }
        public JobTriggerSource Source { get; init; }
        public int? DeferredInstanceId { get; init; }
    }
}
