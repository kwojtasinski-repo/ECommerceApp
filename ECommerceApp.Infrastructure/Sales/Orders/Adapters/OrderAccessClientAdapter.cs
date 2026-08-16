using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Sales.Orders.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sales.Orders.Adapters
{
    internal sealed class OrderAccessClientAdapter : IOrderAccessClient
    {
        private readonly IOrderAccessService _orderAccessService;

        public OrderAccessClientAdapter(IOrderAccessService orderAccessService)
        {
            _orderAccessService = orderAccessService;
        }

        public Task<bool> HasAccessAsync(int orderId, string token, CancellationToken ct = default)
            => _orderAccessService.HasAccessAsync(orderId, token, ct);
    }
}
