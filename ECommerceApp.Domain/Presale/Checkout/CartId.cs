using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Presale.Checkout
{
    public sealed record CartId(int Value) : TypedId<int>(Value);
}
