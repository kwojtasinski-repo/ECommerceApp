using ECommerceApp.Application.Sales.Fulfillment.DTOs;
using ECommerceApp.Application.Sales.Fulfillment.Results;
using ECommerceApp.Application.Sales.Fulfillment.ViewModels;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Fulfillment.Services
{
    public interface IShipmentService
    {
        Task<ShipmentOperationResult> CreateShipmentAsync(CreateShipmentDto dto, CancellationToken ct = default);
        Task<ShipmentOperationResult> MarkAsInTransitAsync(int shipmentId, string trackingNumber, CancellationToken ct = default);
        Task<ShipmentOperationResult> MarkAsDeliveredAsync(int shipmentId, CancellationToken ct = default);
        Task<ShipmentOperationResult> MarkAsFailedAsync(int shipmentId, CancellationToken ct = default);
        Task<ShipmentOperationResult> MarkAsPartiallyDeliveredAsync(int shipmentId, IReadOnlyList<int> deliveredProductIds, CancellationToken ct = default);
        Task<ShipmentDetailsVm?> GetShipmentAsync(int shipmentId, CancellationToken ct = default);
        Task<ShipmentListVm> GetShipmentsByOrderIdAsync(int orderId, CancellationToken ct = default);
    }
}
