using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Application.Sales.Orders.ViewModels;
using ECommerceApp.Domain.Sales.Orders;
using System.Linq;
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

        public async Task<BackofficeOrderListVm> GetOrdersAsync(int pageSize, int pageNo, string searchString, CancellationToken ct = default)
        {
            var result = await _orderService.GetAllOrdersAsync(pageSize, pageNo, searchString, ct);
            return new BackofficeOrderListVm
            {
                Orders = result.Orders.Select(MapToItemVm).ToList(),
                CurrentPage = result.CurrentPage,
                PageSize = result.PageSize,
                TotalCount = result.TotalCount,
                SearchString = result.SearchString
            };
        }

        public async Task<BackofficeOrderDetailVm> GetOrderDetailAsync(int orderId, CancellationToken ct = default)
        {
            var order = await _orderService.GetOrderDetailsAsync(orderId, ct);
            if (order is null)
            {
                return null;
            }

            return new BackofficeOrderDetailVm
            {
                Id = order.Id,
                Number = order.Number,
                Cost = order.Cost,
                Status = order.Status.ToString(),
                CustomerId = order.CustomerId,
                IsPaid = IsPaidStatus(order.Status),
                IsDelivered = IsDeliveredStatus(order.Status)
            };
        }

        public async Task<BackofficeOrderListVm> GetOrdersByCustomerAsync(int customerId, int pageSize, int pageNo, CancellationToken ct = default)
        {
            var all = await _orderService.GetOrdersByCustomerIdAsync(customerId, ct);
            var paged = all
                .Skip((pageNo - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new BackofficeOrderListVm
            {
                Orders = paged.Select(MapToItemVm).ToList(),
                CurrentPage = pageNo,
                PageSize = pageSize,
                TotalCount = all.Count
            };
        }

        private static BackofficeOrderItemVm MapToItemVm(OrderForListVm o) => new()
        {
            Id = o.Id,
            Number = o.Number,
            Cost = o.Cost,
            Status = o.Status.ToString(),
            CustomerName = string.Empty, // not in OrderForListVm — tracked as follow-up
            IsPaid = IsPaidStatus(o.Status)
        };

        private static bool IsPaidStatus(OrderStatus status) =>
            status is OrderStatus.PaymentConfirmed
                   or OrderStatus.PartiallyFulfilled
                   or OrderStatus.Fulfilled
                   or OrderStatus.Refunded;

        private static bool IsDeliveredStatus(OrderStatus status) =>
            status is OrderStatus.Fulfilled or OrderStatus.Refunded;
    }
}
