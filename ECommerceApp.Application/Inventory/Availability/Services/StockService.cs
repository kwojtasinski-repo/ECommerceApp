using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Inventory.Availability.DTOs;
using ECommerceApp.Application.Inventory.Availability.Handlers;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Inventory.Availability;
using System;
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
        private readonly ICheckoutSoftHoldService _softHoldService;
        private readonly IDeferredJobScheduler _deferredScheduler;

        public StockService(
            IStockItemRepository stockItemRepo,
            IReservationRepository reservationRepo,
            IProductSnapshotRepository productSnapshotRepo,
            IPendingStockAdjustmentRepository pendingAdjustmentRepo,
            ICheckoutSoftHoldService softHoldService,
            IDeferredJobScheduler deferredScheduler)
        {
            _stockItemRepo = stockItemRepo;
            _reservationRepo = reservationRepo;
            _productSnapshotRepo = productSnapshotRepo;
            _pendingAdjustmentRepo = pendingAdjustmentRepo;
            _softHoldService = softHoldService;
            _deferredScheduler = deferredScheduler;
        }

        public async Task<StockItemDto> GetByProductIdAsync(int productId, CancellationToken ct = default)
        {
            var stock = await _stockItemRepo.GetByProductIdAsync(productId, ct)
                ?? throw new BusinessException($"Stock not found for product '{productId}'.");

            return new StockItemDto(
                stock.Id?.Value ?? 0,
                stock.ProductId,
                stock.Quantity,
                stock.ReservedQuantity,
                stock.AvailableQuantity);
        }

        public async Task InitializeStockAsync(int productId, int initialQuantity, CancellationToken ct = default)
        {
            var existing = await _stockItemRepo.GetByProductIdAsync(productId, ct);
            if (existing != null)
                throw new BusinessException($"Stock already initialized for product '{productId}'.");

            var (stock, _) = StockItem.Create(productId, initialQuantity);
            await _stockItemRepo.AddAsync(stock, ct);
        }

        public async Task ReserveAsync(ReserveStockDto dto, CancellationToken ct = default)
        {
            var snapshot = await _productSnapshotRepo.GetByProductIdAsync(dto.ProductId, ct)
                ?? throw new BusinessException($"Product snapshot not found for product '{dto.ProductId}'.");

            if (!snapshot.CanBeReserved)
                throw new BusinessException($"Product '{dto.ProductId}' is not available for reservation.");

            if (!snapshot.IsDigital)
            {
                var stock = await _stockItemRepo.GetByProductIdAsync(dto.ProductId, ct)
                    ?? throw new BusinessException($"Stock not found for product '{dto.ProductId}'.");

                stock.Reserve(dto.Quantity);
                await _stockItemRepo.UpdateAsync(stock, ct);
            }

            var reservation = Reservation.Create(dto.ProductId, dto.OrderId, dto.Quantity, dto.ExpiresAt);
            await _reservationRepo.AddAsync(reservation, ct);

            await _softHoldService.RemoveAsync(dto.ProductId, dto.UserId, ct);

            var timeoutEntityId = $"{dto.OrderId}:{dto.ProductId}:{dto.Quantity}";
            await _deferredScheduler.ScheduleAsync(
                PaymentWindowTimeoutJob.JobTaskName, timeoutEntityId, dto.ExpiresAt, ct);
        }

        public async Task ReleaseAsync(int orderId, int productId, int quantity, CancellationToken ct = default)
        {
            var reservation = await _reservationRepo.GetByOrderAndProductAsync(orderId, productId, ct);
            if (reservation is null)
                return;

            if (reservation.Status == ReservationStatus.Guaranteed)
            {
                var stock = await _stockItemRepo.GetByProductIdAsync(productId, ct);
                if (stock != null)
                {
                    stock.Release(quantity);
                    await _stockItemRepo.UpdateAsync(stock, ct);
                }
            }

            await _reservationRepo.DeleteAsync(reservation, ct);
        }

        public async Task ConfirmAsync(int orderId, int productId, CancellationToken ct = default)
        {
            var reservation = await _reservationRepo.GetByOrderAndProductAsync(orderId, productId, ct);
            if (reservation is null)
                return;

            reservation.Confirm();
            await _reservationRepo.UpdateAsync(reservation, ct);
        }

        public async Task FulfillAsync(int orderId, int productId, int quantity, CancellationToken ct = default)
        {
            var stock = await _stockItemRepo.GetByProductIdAsync(productId, ct)
                ?? throw new BusinessException($"Stock not found for product '{productId}'.");

            stock.Fulfill(quantity);
            await _stockItemRepo.UpdateAsync(stock, ct);

            var reservation = await _reservationRepo.GetByOrderAndProductAsync(orderId, productId, ct);
            if (reservation != null)
                await _reservationRepo.DeleteAsync(reservation, ct);
        }

        public async Task ReturnAsync(int productId, int quantity, CancellationToken ct = default)
        {
            var stock = await _stockItemRepo.GetByProductIdAsync(productId, ct)
                ?? throw new BusinessException($"Stock not found for product '{productId}'.");

            stock.Return(quantity);
            await _stockItemRepo.UpdateAsync(stock, ct);
        }

        public async Task AdjustAsync(AdjustStockDto dto, CancellationToken ct = default)
        {
            await _pendingAdjustmentRepo.UpsertAsync(dto.ProductId, dto.NewQuantity, ct);
            await _deferredScheduler.CancelAsync(StockAdjustmentJob.JobTaskName, dto.ProductId.ToString(), ct);
            await _deferredScheduler.ScheduleAsync(
                StockAdjustmentJob.JobTaskName, dto.ProductId.ToString(), DateTime.UtcNow, ct);
        }
    }
}
