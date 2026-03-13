using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Sales.Fulfillment.Contracts;
using ECommerceApp.Application.Sales.Orders.Services;

namespace ECommerceApp.Infrastructure.Sales.Fulfillment.Adapters
{
    internal sealed class OrderExistenceCheckerAdapter : IOrderExistenceChecker
    {
        private readonly IOrderService _orders;

        public OrderExistenceCheckerAdapter(IOrderService orders)
        {
            _orders = orders;
        }

        public async Task<bool> ExistsAsync(int orderId, CancellationToken ct = default)
            => await _orders.GetOrderDetailsAsync(orderId, ct) is not null;
    }
}
