using ECommerceApp.Application.Messaging;

namespace ECommerceApp.Application.Messaging
{
    /// <summary>
    /// Cross-BC query: asks the Orders BC how many fulfilled orders a user has placed.
    /// Used by Coupons (FirstPurchaseOnly rule) via IModuleClient.SendAsync.
    /// </summary>
    public sealed record CompletedOrderCountQuery(string UserId) : IQuery<int>;
}
