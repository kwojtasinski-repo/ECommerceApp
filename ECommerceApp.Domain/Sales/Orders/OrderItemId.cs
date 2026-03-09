using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Orders
{
    public sealed record OrderItemId(int Value) : TypedId<int>(Value);
}
