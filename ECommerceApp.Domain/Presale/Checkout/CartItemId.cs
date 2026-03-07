using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Presale.Checkout
{
    public sealed record CartItemId(int Value) : TypedId<int>(Value);
}
