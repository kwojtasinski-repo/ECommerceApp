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
        private readonly IStockHoldRepository _stockHoldRepo;
        private readonly IProductSnapshotRepository _productSnapshotRepo;
        private readonly IPendingStockAdjustmentRepository _pendingAdjustmentRepo;
        private readonly IDeferredJobScheduler _deferredScheduler;
        private readonly IMessageBroker _broker;
        private readonly IStockAuditRepository _auditRepo;

        public StockService(
            IStockItemRepository stockItemRepo,
            IStockHoldRepository stockHoldRepo,
            IProductSnapshotRepository productSnapshotRepo,
            IPendingStockAdjustmentRepository pendingAdjustmentRepo,
            IDeferredJobScheduler deferredScheduler,
            IMessageBroker broker,
            IStockAuditRepository auditRepo)
        {
            _stockItemRepo = stockItemRepo;
            _stockHoldRepo = stockHoldRepo;
            _productSnapshotRepo = productSnapshotRepo;
            _pendingAdjustmentRepo = pendingAdjustmentRepo;
            _deferredScheduler = deferredScheduler;
            _broker = broker;
            _auditRepo = auditRepo;
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
            await _auditRepo.AddAsync(StockAuditEntry.Create(productId, StockChangeType.Initialized, 0, stock.AvailableQuantity, null, DateTime.UtcNow), ct);
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
                {
                    return ReserveStockResult.StockNotFound;
                }

                if (dto.Quantity > stock.AvailableQuantity)
                {
                    return ReserveStockResult.InsufficientStock;
                }

                var reserveBefore = stock.AvailableQuantity;
                stock.Reserve(dto.Quantity);
                await _stockItemRepo.UpdateAsync(stock, ct);
                await _auditRepo.AddAsync(StockAuditEntry.Create(dto.ProductId, StockChangeType.Reserved, reserveBefore, stock.AvailableQuantity, dto.OrderId, DateTime.UtcNow), ct);
                await _broker.PublishAsync(new StockAvailabilityChanged(dto.ProductId, stock.AvailableQuantity, DateTime.UtcNow));
            }

            var stockHold = StockHold.Create(new StockProductId(dto.ProductId), new ReservationOrderId(dto.OrderId), dto.Quantity, dto.ExpiresAt);
            await _stockHoldRepo.AddAsync(stockHold, ct);

            var timeoutEntityId = $"{dto.OrderId}:{dto.ProductId}:{dto.Quantity}";
            await _deferredScheduler.ScheduleAsync(PaymentWindowTimeoutJob.JobTaskName, timeoutEntityId, dto.ExpiresAt, ct);
            return ReserveStockResult.Success;
        }

        public async Task<bool> ReleaseAsync(int orderId, int productId, int quantity, CancellationToken ct = default)
        {
            var stockHold = await _stockHoldRepo.GetByOrderAndProductAsync(orderId, productId, ct);
            if (stockHold is null)
            {
                return false;
            }

            if (stockHold.IsGuaranteed)
            {
                var stock = await _stockItemRepo.GetByProductIdAsync(productId, ct);
                if (stock != null && quantity <= stock.ReservedQuantity.Value)
                {
                    var releaseBefore = stock.AvailableQuantity;
                    stock.Release(quantity);
                    await _stockItemRepo.UpdateAsync(stock, ct);
                    await _auditRepo.AddAsync(StockAuditEntry.Create(productId, StockChangeType.Released, releaseBefore, stock.AvailableQuantity, orderId, DateTime.UtcNow), ct);
                    await _broker.PublishAsync(new StockAvailabilityChanged(productId, stock.AvailableQuantity, DateTime.UtcNow));
                }
            }

            stockHold.MarkAsReleased();
            await _stockHoldRepo.UpdateAsync(stockHold, ct);
            return true;
        }

        public async Task<bool> ConfirmAsync(int orderId, int productId, CancellationToken ct = default)
        {
            var stockHold = await _stockHoldRepo.GetByOrderAndProductAsync(orderId, productId, ct);
            if (stockHold is null)
            {
                return false;
            }

            stockHold.Confirm();
            await _stockHoldRepo.UpdateAsync(stockHold, ct);
            return true;
        }

        public async Task ConfirmHoldsByOrderAsync(int orderId, CancellationToken ct = default)
        {
            var stockHolds = await _stockHoldRepo.GetByOrderIdAsync(orderId, ct);
            foreach (var stockHold in stockHolds)
            {
                stockHold.Confirm();
                await _stockHoldRepo.UpdateAsync(stockHold, ct);
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

            var fulfillBefore = stock.AvailableQuantity;
            stock.Fulfill(quantity);
            await _stockItemRepo.UpdateAsync(stock, ct);
            await _auditRepo.AddAsync(StockAuditEntry.Create(productId, StockChangeType.Fulfilled, fulfillBefore, stock.AvailableQuantity, orderId, DateTime.UtcNow), ct);
            await _broker.PublishAsync(new StockAvailabilityChanged(productId, stock.AvailableQuantity, DateTime.UtcNow));

            var stockHold = await _stockHoldRepo.GetByOrderAndProductAsync(orderId, productId, ct);
            if (stockHold != null)
            {
                stockHold.MarkAsFulfilled();
                await _stockHoldRepo.UpdateAsync(stockHold, ct);
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

            var returnBefore = stock.AvailableQuantity;
            stock.Return(quantity);
            await _stockItemRepo.UpdateAsync(stock, ct);
            await _auditRepo.AddAsync(StockAuditEntry.Create(productId, StockChangeType.Returned, returnBefore, stock.AvailableQuantity, null, DateTime.UtcNow), ct);
            await _broker.PublishAsync(new StockAvailabilityChanged(productId, stock.AvailableQuantity, DateTime.UtcNow));
            return true;
        }

        public async Task AdjustAsync(AdjustStockDto dto, CancellationToken ct = default)
        {
            await _pendingAdjustmentRepo.UpsertAsync(dto.ProductId, dto.NewQuantity, ct);
            await _deferredScheduler.CancelAsync(StockAdjustmentJob.JobTaskName, dto.ProductId.ToString(), ct);
            await _deferredScheduler.ScheduleAsync(StockAdjustmentJob.JobTaskName, dto.ProductId.ToString(), DateTime.UtcNow, ct);
        }

        public async Task<bool> WithdrawHoldAsync(int orderId, int productId, CancellationToken ct = default)
        {
            var stockHold = await _stockHoldRepo.GetByOrderAndProductAsync(orderId, productId, ct);
            if (stockHold is null)
            {
                return false;
            }

            stockHold.Withdraw();

            var stock = await _stockItemRepo.GetByProductIdAsync(productId, ct);
            if (stock != null && stockHold.Quantity <= stock.ReservedQuantity.Value)
            {
                var before = stock.AvailableQuantity;
                stock.Release(stockHold.Quantity);
                await _stockItemRepo.UpdateAsync(stock, ct);
                await _auditRepo.AddAsync(StockAuditEntry.Create(productId, StockChangeType.Withdrawn, before, stock.AvailableQuantity, orderId, DateTime.UtcNow), ct);
                await _broker.PublishAsync(new StockAvailabilityChanged(productId, stock.AvailableQuantity, DateTime.UtcNow));
            }

            await _stockHoldRepo.UpdateAsync(stockHold, ct);
            return true;
        }

        public async Task ReleaseAllHoldsForOrderAsync(int orderId, CancellationToken ct = default)
        {
            var holds = await _stockHoldRepo.GetByOrderIdAsync(orderId, ct);
            foreach (var hold in holds)
            {
                await ReleaseAsync(orderId, hold.ProductId.Value, hold.Quantity, ct);
            }
        }
    }
}
