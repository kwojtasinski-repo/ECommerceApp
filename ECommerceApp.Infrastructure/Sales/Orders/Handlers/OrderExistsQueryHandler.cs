using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Services;

namespace ECommerceApp.Infrastructure.Sales.Orders.Handlers
{
    /// <summary>
    /// Query handler that checks whether an order exists.
    /// Called cross-BC (e.g., by Coupons, Fulfillment) via IModuleClient.SendAsync.
    /// </summary>
    internal sealed class OrderExistsQueryHandler : IQueryHandler<OrderExistsQuery, bool>
    {
        private readonly IOrderService _orders;

        public OrderExistsQueryHandler(IOrderService orders)
        {
            _orders = orders;
        }

        public async Task<bool> HandleAsync(OrderExistsQuery query, CancellationToken ct = default)
            => await _orders.GetOrderDetailsAsync(query.OrderId, ct) is not null;
    }
}
