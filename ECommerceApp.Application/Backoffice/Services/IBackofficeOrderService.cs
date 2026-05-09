using ECommerceApp.Application.Backoffice.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    public interface IBackofficeOrderService
    {
        Task<BackofficeOrderListVm> GetOrdersAsync(int pageSize, int pageNo, string searchString, CancellationToken ct = default);
        Task<BackofficeOrderDetailVm> GetOrderDetailAsync(int orderId, CancellationToken ct = default);
        Task<BackofficeOrderListVm> GetOrdersByCustomerAsync(int customerId, int pageSize, int pageNo, CancellationToken ct = default);
    }
}
