using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Sales.Fulfillment.Services;
using ECommerceApp.Application.Sales.Orders.Services;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    internal sealed class BackofficeRefundService : IBackofficeRefundService
    {
        private readonly IRefundService _refundService;
        private readonly IOrderService _orderService;

        public BackofficeRefundService(IRefundService refundService, IOrderService orderService)
        {
            _refundService = refundService;
            _orderService = orderService;
        }

        public async Task<BackofficeRefundListVm> GetRefundsAsync(int pageSize, int pageNo, CancellationToken ct = default)
        {
            var source = await _refundService.GetRefundsAsync(pageSize, pageNo, null, ct);
            return new BackofficeRefundListVm
            {
                Refunds = source.Refunds.Select(r => new BackofficeRefundItemVm
                {
                    Id = r.Id,
                    OrderId = r.OrderId,
                    Reason = r.Reason,
                    Status = r.Status,
                    OnWarranty = r.OnWarranty
                }).ToList(),
                CurrentPage = source.CurrentPage,
                PageSize = source.PageSize,
                TotalCount = source.TotalCount
            };
        }

        public async Task<BackofficeRefundDetailVm> GetRefundDetailAsync(int refundId, CancellationToken ct = default)
        {
            var detail = await _refundService.GetRefundAsync(refundId, ct);
            if (detail is null)
                return null;

            var customerId = await _orderService.GetCustomerIdAsync(detail.OrderId, ct) ?? 0;
            return new BackofficeRefundDetailVm
            {
                Id = detail.Id,
                OrderId = detail.OrderId,
                CustomerId = customerId,
                Reason = detail.Reason,
                Status = detail.Status,
                OnWarranty = detail.OnWarranty
            };
        }

        public async Task<BackofficeRefundListVm> GetRefundsByOrderAsync(int orderId, CancellationToken ct = default)
        {
            var refunds = await _refundService.GetByOrderIdAsync(orderId, ct);
            return new BackofficeRefundListVm
            {
                Refunds = refunds.Select(r => new BackofficeRefundItemVm
                {
                    Id = r.Id,
                    OrderId = r.OrderId,
                    Reason = r.Reason,
                    Status = r.Status,
                    OnWarranty = r.OnWarranty
                }).ToList(),
                CurrentPage = 1,
                PageSize = refunds.Count,
                TotalCount = refunds.Count
            };
        }
    }
}

