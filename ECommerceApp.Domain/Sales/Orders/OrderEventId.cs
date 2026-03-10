using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Orders
{
    public sealed record OrderEventId(int Value) : TypedId<int>(Value);
}
