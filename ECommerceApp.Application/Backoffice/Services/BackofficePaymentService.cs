using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Sales.Payments.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    internal sealed class BackofficePaymentService : IBackofficePaymentService
    {
        private readonly IPaymentService _paymentService;

        public BackofficePaymentService(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        public Task<BackofficePaymentListVm> GetPaymentsAsync(int pageSize, int pageNo, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<BackofficePaymentDetailVm?> GetPaymentDetailAsync(int paymentId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<BackofficePaymentListVm> GetUnpaidOrderPaymentsAsync(int pageSize, int pageNo, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
