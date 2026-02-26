using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Supporting.TimeManagement
{
    public sealed record JobExecutionId(int Value) : TypedId<int>(Value);
}
