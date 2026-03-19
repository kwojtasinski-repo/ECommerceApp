using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Contracts;
using ECommerceApp.Application.Sales.Fulfillment.DTOs;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Fulfillment.Results;
using ECommerceApp.Application.Sales.Fulfillment.ViewModels;
using ECommerceApp.Domain.Sales.Fulfillment;
using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Application.Sales.Fulfillment.Services
{
    internal sealed class ShipmentService : IShipmentService
    {
        private readonly IShipmentRepository _shipments;
        private readonly IOrderExistenceChecker _orderExistence;
        private readonly IMessageBroker _broker;

        public ShipmentService(
            IShipmentRepository shipments,
            IOrderExistenceChecker orderExistence,
            IMessageBroker broker)
        {
            _shipments = shipments;
            _orderExistence = orderExistence;
            _broker = broker;
        }

        public async Task<ShipmentOperationResult> CreateShipmentAsync(CreateShipmentDto dto, CancellationToken ct = default)
        {
            if (!await _orderExistence.ExistsAsync(dto.OrderId, ct))
            {
                return ShipmentOperationResult.OrderNotFound;
            }

            var lines = dto.Lines.Select(l => ShipmentLine.Create(l.ProductId, l.Quantity));
            var shipment = Shipment.Create(dto.OrderId, lines);
            await _shipments.AddAsync(shipment, ct);

            return ShipmentOperationResult.Success;
        }

        public async Task<ShipmentOperationResult> MarkAsInTransitAsync(int shipmentId, string trackingNumber, CancellationToken ct = default)
        {
            var shipment = await _shipments.GetByIdAsync(shipmentId, ct);
            if (shipment is null)
            {
                return ShipmentOperationResult.NotFound;
            }

            try
            {
                shipment.MarkAsInTransit(trackingNumber);
            }
            catch (DomainException)
            {
                return ShipmentOperationResult.InvalidStatus;
            }

            await _shipments.UpdateAsync(shipment, ct);

            await _broker.PublishAsync(new ShipmentDispatched(
                shipment.Id.Value,
                shipment.OrderId,
                trackingNumber,
                DateTime.UtcNow));

            return ShipmentOperationResult.Success;
        }

        public async Task<ShipmentOperationResult> MarkAsDeliveredAsync(int shipmentId, CancellationToken ct = default)
        {
            var shipment = await _shipments.GetByIdAsync(shipmentId, ct);
            if (shipment is null)
            {
                return ShipmentOperationResult.NotFound;
            }

            try
            {
                shipment.MarkAsDelivered();
            }
            catch (DomainException)
            {
                return ShipmentOperationResult.InvalidStatus;
            }

            await _shipments.UpdateAsync(shipment, ct);

            await _broker.PublishAsync(new ShipmentDelivered(
                shipment.Id.Value,
                shipment.OrderId,
                DateTime.UtcNow));

            return ShipmentOperationResult.Success;
        }

        public async Task<ShipmentOperationResult> MarkAsFailedAsync(int shipmentId, CancellationToken ct = default)
        {
            var shipment = await _shipments.GetByIdAsync(shipmentId, ct);
            if (shipment is null)
            {
                return ShipmentOperationResult.NotFound;
            }

            try
            {
                shipment.MarkAsFailed();
            }
            catch (DomainException)
            {
                return ShipmentOperationResult.InvalidStatus;
            }

            await _shipments.UpdateAsync(shipment, ct);

            await _broker.PublishAsync(new ShipmentFailed(
                shipment.Id.Value,
                shipment.OrderId,
                DateTime.UtcNow));

            return ShipmentOperationResult.Success;
        }

        public async Task<ShipmentDetailsVm?> GetShipmentAsync(int shipmentId, CancellationToken ct = default)
        {
            var shipment = await _shipments.GetByIdAsync(shipmentId, ct);
            if (shipment is null)
            {
                return null;
            }

            return MapToDetailsVm(shipment);
        }

        public async Task<ShipmentListVm> GetShipmentsByOrderIdAsync(int orderId, CancellationToken ct = default)
        {
            var shipments = await _shipments.GetByOrderIdAsync(orderId, ct);

            return new ShipmentListVm
            {
                Shipments = shipments.Select(MapToVm).ToList()
            };
        }

        private static ShipmentDetailsVm MapToDetailsVm(Shipment shipment)
            => new(
                shipment.Id.Value,
                shipment.OrderId,
                shipment.TrackingNumber,
                shipment.Status.ToString(),
                shipment.ShippedAt,
                shipment.DeliveredAt,
                shipment.Lines.Select(l => new ShipmentLineVm(l.ProductId, l.Quantity)).ToList());

        private static ShipmentVm MapToVm(Shipment shipment)
            => new(
                shipment.Id.Value,
                shipment.OrderId,
                shipment.TrackingNumber,
                shipment.Status.ToString(),
                shipment.ShippedAt,
                shipment.DeliveredAt);
    }
}
