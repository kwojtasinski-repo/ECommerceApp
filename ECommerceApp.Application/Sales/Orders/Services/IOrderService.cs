using ECommerceApp.Application.Sales.Orders.DTOs;
using ECommerceApp.Application.Sales.Orders.Results;
using ECommerceApp.Application.Sales.Orders.ViewModels;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Services
{
    public interface IOrderService
    {
        Task<PlaceOrderResult> PlaceOrderAsync(PlaceOrderDto dto, CancellationToken ct = default);
        Task<PlaceOrderResult> PlaceOrderFromPresaleAsync(PlaceOrderFromPresaleDto dto, CancellationToken ct = default);
        Task<OrderDetailsVm?> GetOrderDetailsAsync(int orderId, CancellationToken ct = default);
        Task<OrderOperationResult> UpdateOrderAsync(UpdateOrderDto dto, CancellationToken ct = default);
        Task<OrderOperationResult> DeleteOrderAsync(int orderId, CancellationToken ct = default);
        Task<OrderOperationResult> MarkAsDeliveredAsync(int orderId, CancellationToken ct = default);
        Task<OrderOperationResult> AddCouponAsync(int orderId, int couponUsedId, int discountPercent, CancellationToken ct = default);
        Task<OrderOperationResult> RemoveCouponAsync(int orderId, CancellationToken ct = default);
        Task<OrderOperationResult> AddRefundAsync(int orderId, int refundId, CancellationToken ct = default);
        Task<OrderOperationResult> RemoveRefundByRefundIdAsync(int refundId, CancellationToken ct = default);
        Task<OrderListVm> GetAllOrdersAsync(int pageSize, int pageNo, string? search, CancellationToken ct = default);
        Task<IReadOnlyList<OrderForListVm>> GetOrdersByUserIdAsync(string userId, CancellationToken ct = default);
        Task<IReadOnlyList<OrderForListVm>> GetOrdersByCustomerIdAsync(int customerId, CancellationToken ct = default);
        Task<OrderListVm> GetAllPaidOrdersAsync(int pageSize, int pageNo, string? search, CancellationToken ct = default);
        Task<int?> GetCustomerIdAsync(int orderId, CancellationToken ct = default);
        Task<OrderOperationResult> MarkAsPaidAsync(int orderId, int paymentId, CancellationToken ct = default);
        Task<OrderOperationResult> CancelOrderAsync(int orderId, CancellationToken ct = default);
    }
}
