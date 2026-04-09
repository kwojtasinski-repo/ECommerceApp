using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Sales.Orders.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    internal sealed class BackofficeOrderService : IBackofficeOrderService
    {
        private readonly IOrderService _orderService;

        public BackofficeOrderService(IOrderService orderService)
        {
            _orderService = orderService;
        }

        public Task<BackofficeOrderListVm> GetOrdersAsync(int pageSize, int pageNo, string? searchString, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<BackofficeOrderDetailVm?> GetOrderDetailAsync(int orderId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<BackofficeOrderListVm> GetOrdersByCustomerAsync(int customerId, int pageSize, int pageNo, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
