using ECommerceApp.Application.Sales.Payments.DTOs;
using ECommerceApp.Application.Sales.Payments.Services;
using ECommerceApp.Application.Sales.Payments.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Payments.Services
{
    public interface IPaymentService
    {
        Task<PaymentDetailsVm?> GetByIdAsync(int paymentId, CancellationToken ct = default);
        Task<PaymentDetailsVm?> GetByOrderIdAsync(int orderId, CancellationToken ct = default);
        Task<PaymentOperationResult> ConfirmAsync(ConfirmPaymentDto dto, CancellationToken ct = default);
    }
}
