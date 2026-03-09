using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Orders
{
    public sealed record OrderId(int Value) : TypedId<int>(Value);
}
