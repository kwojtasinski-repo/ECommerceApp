using ECommerceApp.Application.Inventory.Availability.DTOs;
using ECommerceApp.Application.Inventory.Availability.Handlers;
using ECommerceApp.Application.Inventory.Availability.Messages;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Domain.Inventory.Availability.ValueObjects;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Services
{
    internal sealed class StockService : IStockService
    {
        private readonly IStockItemRepository _stockItemRepo;
        private readonly IReservationRepository _reservationRepo;
        private readonly IProductSnapshotRepository _productSnapshotRepo;
        private readonly IPendingStockAdjustmentRepository _pendingAdjustmentRepo;
        private readonly IDeferredJobScheduler _deferredScheduler;
        private readonly IMessageBroker _broker;

        public StockService(
            IStockItemRepository stockItemRepo,
            IReservationRepository reservationRepo,
            IProductSnapshotRepository productSnapshotRepo,
            IPendingStockAdjustmentRepository pendingAdjustmentRepo,
            IDeferredJobScheduler deferredScheduler,
            IMessageBroker broker)
        {
            _stockItemRepo = stockItemRepo;
            _reservationRepo = reservationRepo;
            _productSnapshotRepo = productSnapshotRepo;
            _pendingAdjustmentRepo = pendingAdjustmentRepo;
            _deferredScheduler = deferredScheduler;
            _broker = broker;
        }

        public async Task<StockItemDto?> GetByProductIdAsync(int productId, CancellationToken ct = default)
        {
            var stock = await _stockItemRepo.GetByProductIdAsync(productId, ct);
            if (stock is null)
            {
                return null;
            }

            return new StockItemDto(
                stock.Id?.Value ?? 0,
                stock.ProductId.Value,
                stock.Quantity.Value,
                stock.ReservedQuantity.Value,
                stock.AvailableQuantity);
        }

        public async IAsyncEnumerable<StockItemDto> GetByProductIdsAsync(
            IReadOnlyList<int> productIds,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var s in _stockItemRepo.GetByProductIdsAsync(productIds, ct))
                yield return new StockItemDto(
                    s.Id?.Value ?? 0,
                    s.ProductId.Value,
                    s.Quantity.Value,
                    s.ReservedQuantity.Value,
                    s.AvailableQuantity);
        }

        public async Task<bool> InitializeStockAsync(int productId, int initialQuantity, CancellationToken ct = default)
        {
            var existing = await _stockItemRepo.GetByProductIdAsync(productId, ct);
            if (existing != null)
            {
                return false;
            }

            var (stock, _) = StockItem.Create(new StockProductId(productId), new StockQuantity(initialQuantity));
            await _stockItemRepo.AddAsync(stock, ct);
            await _broker.PublishAsync(new StockAvailabilityChanged(productId, stock.AvailableQuantity, DateTime.UtcNow));
            return true;
        }

        public async Task<ReserveStockResult> ReserveAsync(ReserveStockDto dto, CancellationToken ct = default)
        {
            var snapshot = await _productSnapshotRepo.GetByProductIdAsync(dto.ProductId, ct);
            if (snapshot is null)
            {
                return ReserveStockResult.ProductSnapshotNotFound;
            }

            if (!snapshot.CanBeReserved)
            {
                return ReserveStockResult.ProductNotAvailable;
            }

            if (!snapshot.IsDigital)
            {
                var stock = await _stockItemRepo.GetByProductIdAsync(dto.ProductId, ct);
                if (stock is null)
                    return ReserveStockResult.StockNotFound;

                if (dto.Quantity > stock.AvailableQuantity)
                    return ReserveStockResult.InsufficientStock;

                stock.Reserve(dto.Quantity);
                await _stockItemRepo.UpdateAsync(stock, ct);
                await _broker.PublishAsync(new StockAvailabilityChanged(dto.ProductId, stock.AvailableQuantity, DateTime.UtcNow));
            }

            var reservation = Reservation.Create(new StockProductId(dto.ProductId), new ReservationOrderId(dto.OrderId), dto.Quantity, dto.ExpiresAt);
            await _reservationRepo.AddAsync(reservation, ct);

            var timeoutEntityId = $"{dto.OrderId}:{dto.ProductId}:{dto.Quantity}";
            await _deferredScheduler.ScheduleAsync(PaymentWindowTimeoutJob.JobTaskName, timeoutEntityId, dto.ExpiresAt, ct);
            return ReserveStockResult.Success;
        }

        public async Task<bool> ReleaseAsync(int orderId, int productId, int quantity, CancellationToken ct = default)
        {
            var reservation = await _reservationRepo.GetByOrderAndProductAsync(orderId, productId, ct);
            if (reservation is null)
            {
                return false;
            }

            if (reservation.IsGuaranteed)
            {
                var stock = await _stockItemRepo.GetByProductIdAsync(productId, ct);
                if (stock != null && quantity <= stock.ReservedQuantity.Value)
                {
                    stock.Release(quantity);
                    await _stockItemRepo.UpdateAsync(stock, ct);
                    await _broker.PublishAsync(new StockAvailabilityChanged(productId, stock.AvailableQuantity, DateTime.UtcNow));
                }
            }

            await _reservationRepo.DeleteAsync(reservation, ct);
            return true;
        }

        public async Task<bool> ConfirmAsync(int orderId, int productId, CancellationToken ct = default)
        {
            var reservation = await _reservationRepo.GetByOrderAndProductAsync(orderId, productId, ct);
            if (reservation is null)
            {
                return false;
            }

            reservation.Confirm();
            await _reservationRepo.UpdateAsync(reservation, ct);
            return true;
        }

        public async Task ConfirmReservationsByOrderAsync(int orderId, CancellationToken ct = default)
        {
            var reservations = await _reservationRepo.GetByOrderIdAsync(orderId, ct);
            foreach (var reservation in reservations)
            {
                reservation.Confirm();
                await _reservationRepo.UpdateAsync(reservation, ct);
            }
        }

        public async Task<bool> FulfillAsync(int orderId, int productId, int quantity, CancellationToken ct = default)
        {
            var stock = await _stockItemRepo.GetByProductIdAsync(productId, ct);
            if (stock is null)
            {
                return false;
            }

            if (quantity > stock.ReservedQuantity.Value)
            {
                return false;
            }

            stock.Fulfill(quantity);
            await _stockItemRepo.UpdateAsync(stock, ct);
            await _broker.PublishAsync(new StockAvailabilityChanged(productId, stock.AvailableQuantity, DateTime.UtcNow));

            var reservation = await _reservationRepo.GetByOrderAndProductAsync(orderId, productId, ct);
            if (reservation != null)
            {
                await _reservationRepo.DeleteAsync(reservation, ct);
            }

            return true;
        }

        public async Task<bool> ReturnAsync(int productId, int quantity, CancellationToken ct = default)
        {
            var stock = await _stockItemRepo.GetByProductIdAsync(productId, ct);
            if (stock is null)
            {
                return false;
            }

            stock.Return(quantity);
            await _stockItemRepo.UpdateAsync(stock, ct);
            await _broker.PublishAsync(new StockAvailabilityChanged(productId, stock.AvailableQuantity, DateTime.UtcNow));
            return true;
        }

        public async Task AdjustAsync(AdjustStockDto dto, CancellationToken ct = default)
        {
            await _pendingAdjustmentRepo.UpsertAsync(dto.ProductId, dto.NewQuantity, ct);
            await _deferredScheduler.CancelAsync(StockAdjustmentJob.JobTaskName, dto.ProductId.ToString(), ct);
            await _deferredScheduler.ScheduleAsync(StockAdjustmentJob.JobTaskName, dto.ProductId.ToString(), DateTime.UtcNow, ct);
        }
    }
}
