using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Fulfillment
{
    public sealed record ShipmentId(int Value) : TypedId<int>(Value);
}
