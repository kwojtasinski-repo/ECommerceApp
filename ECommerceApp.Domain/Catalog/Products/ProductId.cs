using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Catalog.Products
{
    public sealed record ProductId(int Value) : TypedId<int>(Value);
}
