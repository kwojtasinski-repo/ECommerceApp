using ECommerceApp.Application.Backoffice.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    public interface IBackofficeRefundService
    {
        Task<BackofficeRefundListVm> GetRefundsAsync(int pageSize, int pageNo, CancellationToken ct = default);
        Task<BackofficeRefundDetailVm> GetRefundDetailAsync(int refundId, CancellationToken ct = default);
        Task<BackofficeRefundListVm> GetRefundsByOrderAsync(int orderId, CancellationToken ct = default);
    }
}
