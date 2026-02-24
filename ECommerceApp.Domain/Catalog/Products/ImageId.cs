using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Catalog.Products
{
    public sealed record ImageId(int Value) : TypedId<int>(Value);
}
