using ECommerceApp.Application.Sales.Fulfillment.DTOs;
using ECommerceApp.Application.Sales.Fulfillment.Results;
using ECommerceApp.Application.Sales.Fulfillment.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Fulfillment.Services
{
    public interface IRefundService
    {
        Task<RefundRequestResult> RequestRefundAsync(RequestRefundDto dto, CancellationToken ct = default);
        Task<RefundOperationResult> ApproveRefundAsync(int refundId, CancellationToken ct = default);
        Task<RefundOperationResult> RejectRefundAsync(int refundId, CancellationToken ct = default);
        Task<RefundDetailsVm?> GetRefundAsync(int refundId, CancellationToken ct = default);
        Task<RefundListVm> GetRefundsAsync(int pageSize, int pageNo, string? search, CancellationToken ct = default);
    }
}
