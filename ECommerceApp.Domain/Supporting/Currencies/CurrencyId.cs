using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Supporting.Currencies
{
    public sealed record CurrencyId(int Value) : TypedId<int>(Value);
}
