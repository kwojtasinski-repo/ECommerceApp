using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Domain.Sales.Orders;

namespace ECommerceApp.Infrastructure.Sales.Orders.Handlers
{
    internal sealed class CompletedOrderCountQueryHandler : IQueryHandler<CompletedOrderCountQuery, int>
    {
        private readonly IOrderService _orders;

        public CompletedOrderCountQueryHandler(IOrderService orders)
        {
            _orders = orders;
        }

        public async Task<int> HandleAsync(CompletedOrderCountQuery query, CancellationToken ct = default)
        {
            var orders = await _orders.GetOrdersByUserIdAsync(query.UserId, ct);
            return orders.Count(o => o.Status == OrderStatus.Fulfilled);
        }
    }
}
