using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Orders
{
    public sealed record OrderUserId(string Value) : TypedId<string>(Value)
    {
        public static implicit operator OrderUserId(string value) => new(value);
    }
}
