using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.AccountProfile
{
    public sealed record UserProfileId(int Value) : TypedId<int>(Value);
}
