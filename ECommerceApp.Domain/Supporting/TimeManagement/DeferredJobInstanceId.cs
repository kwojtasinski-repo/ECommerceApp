using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Supporting.TimeManagement
{
    public sealed record DeferredJobInstanceId(int Value) : TypedId<int>(Value);
}
