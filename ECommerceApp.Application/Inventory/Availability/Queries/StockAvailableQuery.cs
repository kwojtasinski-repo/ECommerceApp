using ECommerceApp.Application.Messaging;

namespace ECommerceApp.Application.Messaging
{
    /// <summary>
    /// Cross-BC query: asks the Inventory BC whether a product has enough stock.
    /// </summary>
    public sealed record StockAvailableQuery(int ProductId, int Quantity) : IQuery<bool>;
}
