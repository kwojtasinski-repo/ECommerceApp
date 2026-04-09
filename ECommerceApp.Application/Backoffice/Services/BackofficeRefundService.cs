using ECommerceApp.Application.Backoffice.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    internal sealed class BackofficeRefundService : IBackofficeRefundService
    {
        public Task<BackofficeRefundListVm> GetRefundsAsync(int pageSize, int pageNo, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<BackofficeRefundDetailVm?> GetRefundDetailAsync(int refundId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<BackofficeRefundListVm> GetRefundsByOrderAsync(int orderId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
