using ECommerceApp.Application.Inventory.Availability.ViewModels;
using ECommerceApp.Domain.Inventory.Availability;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Services
{
    internal sealed class StockQueryService : IStockQueryService
    {
        private readonly IStockItemRepository _stockItemRepo;
        private readonly IProductSnapshotRepository _snapshotRepo;
        private readonly IPendingStockAdjustmentRepository _pendingRepo;
        private readonly IStockHoldRepository _stockHoldRepo;
        private readonly IStockAuditRepository _auditRepo;
        private readonly IMemoryCache _cache;

        private static readonly TimeSpan OverviewTtl = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan AuditTtl = TimeSpan.FromHours(1);

        public StockQueryService(
            IStockItemRepository stockItemRepo,
            IProductSnapshotRepository snapshotRepo,
            IPendingStockAdjustmentRepository pendingRepo,
            IStockHoldRepository stockHoldRepo,
            IStockAuditRepository auditRepo,
            IMemoryCache cache)
        {
            _stockItemRepo = stockItemRepo;
            _snapshotRepo = snapshotRepo;
            _pendingRepo = pendingRepo;
            _stockHoldRepo = stockHoldRepo;
            _auditRepo = auditRepo;
            _cache = cache;
        }

        public async Task<StockOverviewVm> GetOverviewAsync(int page, int pageSize, CancellationToken ct = default)
        {
            var cacheKey = $"inventory:overview:p{page}:s{pageSize}";
            if (_cache.TryGetValue(cacheKey, out StockOverviewVm? cached) && cached is not null)
            {
                return cached;
            }

            var items = await _stockItemRepo.GetAllPagedAsync(page, pageSize, ct);
            var totalCount = await _stockItemRepo.GetAllCountAsync(ct);

            var productIds = items.Select(s => s.ProductId.Value).ToList();
            var snapshots = await _snapshotRepo.GetByProductIdsAsync(productIds, ct);
            var pending = await _pendingRepo.GetByProductIdsAsync(productIds, ct);

            var snapshotMap = snapshots.ToDictionary(s => s.ProductId);
            var pendingMap = pending.ToDictionary(p => p.ProductId.Value);

            var vmItems = items.Select(s =>
            {
                snapshotMap.TryGetValue(s.ProductId.Value, out var snapshot);
                pendingMap.TryGetValue(s.ProductId.Value, out var adj);
                return new StockOverviewItemVm
                {
                    ProductId             = s.ProductId.Value,
                    ProductName           = snapshot?.ProductName ?? $"Product #{s.ProductId.Value}",
                    Quantity              = s.Quantity.Value,
                    ReservedQuantity      = s.ReservedQuantity.Value,
                    AvailableQuantity     = s.AvailableQuantity,
                    IsDigital             = snapshot?.IsDigital ?? false,
                    CatalogStatus         = snapshot?.CatalogStatus.ToString() ?? "—",
                    HasPendingAdjustment  = adj is not null,
                    PendingNewQuantity    = adj?.NewQuantity.Value,
                };
            }).ToList();

            var vm = new StockOverviewVm
            {
                Items      = vmItems,
                TotalCount = totalCount,
                Page       = page,
                PageSize   = pageSize,
            };

            _cache.Set(cacheKey, vm, OverviewTtl);
            return vm;
        }

        public async Task<StockHoldsVm> GetHoldsAsync(int page, int pageSize, string statusFilter = "active", CancellationToken ct = default)
        {
            var statuses = statusFilter == "active"
                ? new[] { StockHoldStatus.Guaranteed, StockHoldStatus.Confirmed }
                : null;

            var holds = await _stockHoldRepo.GetPagedAsync(page, pageSize, statuses, ct);
            var totalCount = await _stockHoldRepo.GetCountAsync(statuses, ct);

            var productIds = holds.Select(h => h.ProductId.Value).Distinct().ToList();
            var snapshots = await _snapshotRepo.GetByProductIdsAsync(productIds, ct);
            var snapshotMap = snapshots.ToDictionary(s => s.ProductId);

            var vmItems = holds.Select(h =>
            {
                snapshotMap.TryGetValue(h.ProductId.Value, out var snapshot);
                return new StockHoldRowVm
                {
                    Id          = h.Id?.Value ?? 0,
                    ProductId   = h.ProductId.Value,
                    ProductName = snapshot?.ProductName ?? $"Product #{h.ProductId.Value}",
                    OrderId     = h.OrderId.Value,
                    Quantity    = h.Quantity,
                    Status      = h.Status.ToString(),
                    ReservedAt  = h.ReservedAt,
                    ExpiresAt   = h.ExpiresAt,
                    CanRelease  = h.Status == StockHoldStatus.Guaranteed,
                    CanConfirm  = h.Status == StockHoldStatus.Guaranteed,
                    CanWithdraw = h.Status == StockHoldStatus.Guaranteed || h.Status == StockHoldStatus.Confirmed,
                };
            }).ToList();

            return new StockHoldsVm
            {
                Items        = vmItems,
                TotalCount   = totalCount,
                Page         = page,
                PageSize     = pageSize,
                StatusFilter = statusFilter,
            };
        }

        public async Task<StockAuditVm> GetAuditAsync(int page, int pageSize, CancellationToken ct = default)
        {
            var cacheKey = $"inventory:audit:p{page}:s{pageSize}";
            if (page > 1 && _cache.TryGetValue(cacheKey, out StockAuditVm? cached) && cached is not null)
            {
                return cached;
            }

            var (entries, totalCount) = await _auditRepo.GetPagedAsync(page, pageSize, ct);

            var productIds = entries.Select(e => e.ProductId).Distinct().ToList();
            var snapshots = await _snapshotRepo.GetByProductIdsAsync(productIds, ct);
            var snapshotMap = snapshots.ToDictionary(s => s.ProductId);

            var vmItems = entries.Select(e =>
            {
                snapshotMap.TryGetValue(e.ProductId, out var snapshot);
                return new StockAuditRowVm
                {
                    Id             = e.Id?.Value ?? 0,
                    ProductId      = e.ProductId,
                    ProductName    = snapshot?.ProductName ?? $"Product #{e.ProductId}",
                    ChangeType     = e.ChangeType.ToString(),
                    QuantityBefore = e.QuantityBefore,
                    QuantityAfter  = e.QuantityAfter,
                    Delta          = e.Delta,
                    OrderId        = e.OrderId,
                    OccurredAt     = e.OccurredAt,
                };
            }).ToList();

            var vm = new StockAuditVm
            {
                Products      = vmItems,
                TotalCount = totalCount,
                Page       = page,
                PageSize   = pageSize,
            };

            if (page > 1)
            {
                _cache.Set(cacheKey, vm, AuditTtl);
            }

            return vm;
        }

        public async Task<PendingAdjustmentsVm> GetPendingAdjustmentsAsync(CancellationToken ct = default)
        {
            var pending = await _pendingRepo.GetAllAsync(ct);
            var productIds = pending.Select(p => p.ProductId.Value).ToList();
            var snapshots = await _snapshotRepo.GetByProductIdsAsync(productIds, ct);
            var snapshotMap = snapshots.ToDictionary(s => s.ProductId);

            var vmItems = pending.Select(p =>
            {
                snapshotMap.TryGetValue(p.ProductId.Value, out var snapshot);
                return new PendingAdjustmentRowVm
                {
                    ProductId   = p.ProductId.Value,
                    ProductName = snapshot?.ProductName ?? $"Product #{p.ProductId.Value}",
                    NewQuantity = p.NewQuantity.Value,
                    SubmittedAt = p.SubmittedAt,
                    Version     = p.Version,
                };
            }).ToList();

            return new PendingAdjustmentsVm { Items = vmItems };
        }
    }
}
