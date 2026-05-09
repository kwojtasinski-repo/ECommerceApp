using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Application.Sales.Payments.Services;
using ECommerceApp.Application.Sales.Payments.ViewModels;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    internal sealed class BackofficePaymentService : IBackofficePaymentService
    {
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;

        public BackofficePaymentService(IPaymentService paymentService, IOrderService orderService)
        {
            _paymentService = paymentService;
            _orderService = orderService;
        }

        public async Task<BackofficePaymentListVm> GetPaymentsAsync(int pageSize, int pageNo, CancellationToken ct = default)
        {
            var source = await _paymentService.GetAllAsync(pageSize, pageNo, ct);
            return MapToListVm(source);
        }

        public async Task<BackofficePaymentDetailVm> GetPaymentDetailAsync(int paymentId, CancellationToken ct = default)
        {
            var detail = await _paymentService.GetByIdAsync(paymentId, ct);
            if (detail is null)
                return null;

            var customerId = await _orderService.GetCustomerIdAsync(detail.OrderId, ct) ?? 0;
            return new BackofficePaymentDetailVm
            {
                Id = detail.Id,
                Number = detail.PaymentId.ToString(),
                Cost = detail.TotalAmount,
                State = detail.Status,
                OrderId = detail.OrderId,
                CustomerId = customerId
            };
        }

        public async Task<BackofficePaymentListVm> GetUnpaidOrderPaymentsAsync(int pageSize, int pageNo, CancellationToken ct = default)
        {
            var source = await _paymentService.GetAllUnpaidAsync(pageSize, pageNo, ct);
            return MapToListVm(source);
        }

        private static BackofficePaymentListVm MapToListVm(PaymentListVm source)
            => new BackofficePaymentListVm
            {
                Payments = source.Payments.Select(p => new BackofficePaymentItemVm
                {
                    Id = p.Id,
                    Number = string.Empty,
                    Cost = p.TotalAmount,
                    State = p.Status,
                    OrderId = p.OrderId
                }).ToList(),
                CurrentPage = source.CurrentPage,
                PageSize = source.PageSize,
                TotalCount = source.TotalCount
            };
    }
}

