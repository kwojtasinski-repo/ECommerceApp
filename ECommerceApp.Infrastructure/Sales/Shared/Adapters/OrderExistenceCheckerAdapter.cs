using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Application.Sales.Shared.Contracts;

namespace ECommerceApp.Infrastructure.Sales.Shared.Adapters
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
