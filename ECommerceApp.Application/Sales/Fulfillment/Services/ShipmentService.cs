using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment;
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
        private readonly IModuleClient _moduleClient;
        private readonly IFulfillmentUnitOfWork _unitOfWork;
        private readonly IOutboxWriter _outboxWriter;

        public ShipmentService(
            IShipmentRepository shipments,
            IModuleClient moduleClient,
            IFulfillmentUnitOfWork unitOfWork,
            IOutboxWriter outboxWriter)
        {
            _shipments = shipments;
            _moduleClient = moduleClient;
            _unitOfWork = unitOfWork;
            _outboxWriter = outboxWriter;
        }

        public async Task<ShipmentOperationResult> CreateShipmentAsync(CreateShipmentDto dto, CancellationToken ct = default)
        {
            if (!await _moduleClient.SendAsync(new OrderExistsQuery(dto.OrderId), ct))
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

            var transaction = await _unitOfWork.BeginTransactionAsync(CancellationToken.None);
            await using (transaction)
            {
                await _shipments.UpdateAsync(shipment, ct);

                await _outboxWriter.EnqueueAsync(
                    new ShipmentDispatched(shipment.Id.Value, shipment.OrderId, trackingNumber, DateTime.UtcNow),
                    transaction,
                    CancellationToken.None);
                await transaction.CommitAsync(CancellationToken.None);
            }

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

            var transaction = await _unitOfWork.BeginTransactionAsync(CancellationToken.None);
            await using (transaction)
            {
                await _shipments.UpdateAsync(shipment, ct);

                await _outboxWriter.EnqueueAsync(
                    new ShipmentDelivered(shipment.Id.Value, shipment.OrderId, MapToLineItems(shipment.Lines), DateTime.UtcNow),
                    transaction,
                    CancellationToken.None);
                await transaction.CommitAsync(CancellationToken.None);
            }

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

            var transaction = await _unitOfWork.BeginTransactionAsync(CancellationToken.None);
            await using (transaction)
            {
                await _shipments.UpdateAsync(shipment, ct);

                await _outboxWriter.EnqueueAsync(
                    new ShipmentFailed(shipment.Id.Value, shipment.OrderId, MapToLineItems(shipment.Lines), DateTime.UtcNow),
                    transaction,
                    CancellationToken.None);
                await transaction.CommitAsync(CancellationToken.None);
            }

            return ShipmentOperationResult.Success;
        }

        public async Task<ShipmentOperationResult> MarkAsPartiallyDeliveredAsync(int shipmentId, IReadOnlyList<int> deliveredProductIds, CancellationToken ct = default)
        {
            var shipment = await _shipments.GetByIdAsync(shipmentId, ct);
            if (shipment is null)
            {
                return ShipmentOperationResult.NotFound;
            }

            try
            {
                shipment.MarkAsPartiallyDelivered(deliveredProductIds);
            }
            catch (DomainException)
            {
                return ShipmentOperationResult.InvalidStatus;
            }

            var deliveredSet = new HashSet<int>(deliveredProductIds);
            var deliveredItems = shipment.Lines
                .Where(l => deliveredSet.Contains(l.ProductId))
                .Select(l => new ShipmentLineItem(l.ProductId, l.Quantity))
                .ToList();
            var failedItems = shipment.Lines
                .Where(l => !deliveredSet.Contains(l.ProductId))
                .Select(l => new ShipmentLineItem(l.ProductId, l.Quantity))
                .ToList();

            var transaction = await _unitOfWork.BeginTransactionAsync(CancellationToken.None);
            await using (transaction)
            {
                await _shipments.UpdateAsync(shipment, ct);

                await _outboxWriter.EnqueueAsync(
                    new ShipmentPartiallyDelivered(shipment.Id.Value, shipment.OrderId, deliveredItems, failedItems, DateTime.UtcNow),
                    transaction,
                    CancellationToken.None);
                await transaction.CommitAsync(CancellationToken.None);
            }

            return ShipmentOperationResult.Success;
        }

        public async Task<ShipmentDetailsVm> GetShipmentAsync(int shipmentId, CancellationToken ct = default)
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

        public async Task<ShipmentListVm> GetAllShipmentsAsync(int pageSize, int pageNo, string searchString, CancellationToken ct = default)
        {
            var shipments = await _shipments.GetAllAsync(pageSize, pageNo, searchString, ct);
            var total = await _shipments.CountAsync(searchString, ct);
            return new ShipmentListVm
            {
                Shipments = shipments.Select(MapToVm).ToList(),
                CurrentPage = pageNo,
                PageSize = pageSize,
                TotalCount = total,
                SearchString = searchString
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

        private static IReadOnlyList<ShipmentLineItem> MapToLineItems(IReadOnlyList<ShipmentLine> lines)
            => lines.Select(l => new ShipmentLineItem(l.ProductId, l.Quantity)).ToList();
    }
}
