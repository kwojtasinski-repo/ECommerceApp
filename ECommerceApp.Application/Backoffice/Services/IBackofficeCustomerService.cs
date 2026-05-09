using ECommerceApp.Application.Backoffice.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    public interface IBackofficeCustomerService
    {
        Task<BackofficeCustomerListVm> GetCustomersAsync(int pageSize, int pageNo, string searchString, CancellationToken ct = default);
        Task<BackofficeCustomerDetailVm> GetCustomerDetailAsync(int customerId, CancellationToken ct = default);
        Task<BackofficeOrderListVm> GetOrdersByCustomerAsync(int customerId, int pageSize, int pageNo, CancellationToken ct = default);
    }
}
