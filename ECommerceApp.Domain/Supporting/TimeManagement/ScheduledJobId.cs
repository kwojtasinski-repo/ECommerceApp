using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Supporting.TimeManagement
{
    public sealed record ScheduledJobId(int Value) : TypedId<int>(Value);
}
