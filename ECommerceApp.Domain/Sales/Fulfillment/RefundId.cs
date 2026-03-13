using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Fulfillment
{
    public sealed record RefundId(int Value) : TypedId<int>(Value);
}
