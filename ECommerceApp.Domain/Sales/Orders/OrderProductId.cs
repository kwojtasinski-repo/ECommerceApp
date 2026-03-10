using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Orders
{
    public sealed record OrderProductId(int Value) : TypedId<int>(Value)
    {
        public static implicit operator OrderProductId(int value) => new(value);
    }
}
