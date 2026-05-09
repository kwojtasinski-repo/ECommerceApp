using ECommerceApp.Application.Backoffice.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    public interface IBackofficePaymentService
    {
        Task<BackofficePaymentListVm> GetPaymentsAsync(int pageSize, int pageNo, CancellationToken ct = default);
        Task<BackofficePaymentDetailVm> GetPaymentDetailAsync(int paymentId, CancellationToken ct = default);
        Task<BackofficePaymentListVm> GetUnpaidOrderPaymentsAsync(int pageSize, int pageNo, CancellationToken ct = default);
    }
}
