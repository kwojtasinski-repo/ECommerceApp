using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Catalog.Products
{
    public sealed record ItemId(int Value) : TypedId<int>(Value);
}
