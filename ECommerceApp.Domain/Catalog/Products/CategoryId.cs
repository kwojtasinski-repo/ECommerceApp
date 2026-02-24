using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Catalog.Products
{
    public sealed record CategoryId(int Value) : TypedId<int>(Value);
}
