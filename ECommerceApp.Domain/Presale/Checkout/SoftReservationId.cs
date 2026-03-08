using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Presale.Checkout
{
    public sealed record SoftReservationId(int Value) : TypedId<int>(Value);
}
