using ECommerceApp.Application.Backoffice.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    internal sealed class BackofficeCustomerService : IBackofficeCustomerService
    {
        public Task<BackofficeCustomerListVm> GetCustomersAsync(int pageSize, int pageNo, string? searchString, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<BackofficeCustomerDetailVm?> GetCustomerDetailAsync(int customerId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<BackofficeOrderListVm> GetOrdersByCustomerAsync(int customerId, int pageSize, int pageNo, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
