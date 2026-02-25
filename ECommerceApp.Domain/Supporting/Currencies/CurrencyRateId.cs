using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Supporting.Currencies
{
    public sealed record CurrencyRateId(int Value) : TypedId<int>(Value);
}
