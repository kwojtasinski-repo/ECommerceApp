using ECommerceApp.Application.Sales.Payments.DTOs;
using ECommerceApp.Application.Sales.Payments.Services;
using ECommerceApp.Application.Sales.Payments.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Payments.Services
{
    public interface IPaymentService
    {
        Task<PaymentDetailsVm?> GetByIdAsync(int paymentId, CancellationToken ct = default);
        Task<PaymentDetailsVm?> GetByOrderIdAsync(int orderId, CancellationToken ct = default);
        Task<PaymentDetailsVm?> GetByTokenAsync(Guid paymentId, string userId, CancellationToken ct = default);
        Task<PaymentDetailsVm?> GetPendingByOrderIdAsync(int orderId, string userId, CancellationToken ct = default);
        Task<IReadOnlyList<PaymentVm>> GetByUserIdAsync(string userId, CancellationToken ct = default);
        Task<PaymentOperationResult> ConfirmAsync(ConfirmPaymentDto dto, CancellationToken ct = default);
        Task<PaymentOperationResult> ProcessRefundAsync(int orderId, int refundId, CancellationToken ct = default);
    }
}
