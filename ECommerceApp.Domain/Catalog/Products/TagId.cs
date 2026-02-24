using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Catalog.Products
{
    public sealed record TagId(int Value) : TypedId<int>(Value);
}
