using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Sales.Coupons.Contracts;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Domain.Sales.Orders;

namespace ECommerceApp.Infrastructure.Sales.Coupons.Adapters
{
    internal sealed class CompletedOrderCounterAdapter : ICompletedOrderCounter
    {
        private readonly IOrderService _orders;

        public CompletedOrderCounterAdapter(IOrderService orders)
        {
            _orders = orders;
        }

        public async Task<int> CountByUserAsync(string userId, CancellationToken ct = default)
        {
            var orders = await _orders.GetOrdersByUserIdAsync(userId, ct);
            return orders.Count(o => o.Status == OrderStatus.Fulfilled);
        }
    }
}
