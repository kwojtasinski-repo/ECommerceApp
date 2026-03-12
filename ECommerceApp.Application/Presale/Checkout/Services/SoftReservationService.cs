using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.Handlers;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    internal sealed class SoftReservationService : ISoftReservationService
    {
        private readonly ISoftReservationRepository _reservationRepo;
        private readonly IStockSnapshotRepository _snapshotRepo;
        private readonly ICatalogClient _catalogClient;
        private readonly IDeferredJobScheduler _deferredScheduler;
        private readonly IMemoryCache _cache;
        private readonly IOptionsMonitor<PresaleOptions> _options;
        private readonly ConcurrentDictionary<int, HashSet<string>> _productUserIndex = new();

        public SoftReservationService(
            ISoftReservationRepository reservationRepo,
            IStockSnapshotRepository snapshotRepo,
            ICatalogClient catalogClient,
            IDeferredJobScheduler deferredScheduler,
            IMemoryCache cache,
            IOptionsMonitor<PresaleOptions> options)
        {
            _reservationRepo = reservationRepo;
            _snapshotRepo = snapshotRepo;
            _catalogClient = catalogClient;
            _deferredScheduler = deferredScheduler;
            _cache = cache;
            _options = options;
        }

        public async Task<bool> HoldAsync(int productId, string userId, int quantity, CancellationToken ct = default)
        {
            var snapshot = await _snapshotRepo.FindByProductIdAsync(productId, ct);
            if (snapshot is null)
            {
                return false;
            }

            var active = await _reservationRepo.GetByProductIdAsync(productId, ct);
            var reserved = active.Sum(r => r.Quantity.Value);
            if (snapshot.AvailableQuantity - reserved < quantity)
            {
                return false;
            }

            var unitPrice = await _catalogClient.GetUnitPriceAsync(productId, ct);
            if (unitPrice is null)
            {
                return false;
            }

            var ttl = _options.CurrentValue.SoftReservationTtl;
            var expiresAt = DateTime.UtcNow.Add(ttl);
            var reservation = SoftReservation.Create(productId, userId, quantity, unitPrice.Value, expiresAt);
            await _reservationRepo.AddAsync(reservation, ct);

            await _deferredScheduler.ScheduleAsync(SoftReservationExpiredJob.JobTaskName, reservation.Id?.Value.ToString() ?? "", expiresAt, ct);

            _cache.Set(CacheKey(productId, userId), reservation, ttl);
            _productUserIndex.AddOrUpdate(
                productId,
                _ => new HashSet<string> { userId },
                (_, set) => { lock (set) { set.Add(userId); } return set; });

            return true;
        }

        public Task<SoftReservation?> GetAsync(int productId, string userId, CancellationToken ct = default)
        {
            _cache.TryGetValue<SoftReservation>(CacheKey(productId, userId), out var reservation);
            return Task.FromResult(reservation);
        }

        public async Task RemoveAsync(int productId, string userId, CancellationToken ct = default)
        {
            var reservation = await _reservationRepo.FindAsync(productId, userId, ct);
            if (reservation is not null)
            {
                await _deferredScheduler.CancelAsync(SoftReservationExpiredJob.JobTaskName, reservation.Id?.Value.ToString() ?? "", ct);
                await _reservationRepo.DeleteAsync(reservation, ct);
            }

            _cache.Remove(CacheKey(productId, userId));
            if (_productUserIndex.TryGetValue(productId, out var set))
                lock (set) { set.Remove(userId); }
        }

        public async Task RemoveAllForProductAsync(int productId, CancellationToken ct = default)
        {
            var all = await _reservationRepo.GetByProductIdAsync(productId, ct);
            foreach (var r in all)
            {
                await _deferredScheduler.CancelAsync(SoftReservationExpiredJob.JobTaskName, r.Id?.Value.ToString() ?? "", ct);
            }

            await _reservationRepo.DeleteAllForProductAsync(productId, ct);

            if (!_productUserIndex.TryRemove(productId, out var set))
                return;

            lock (set)
            {
                foreach (var uid in set)
                    _cache.Remove(CacheKey(productId, uid));
            }
        }

        public async Task RemoveAllForUserAsync(string userId, CancellationToken ct = default)
        {
            PresaleUserId presaleUserId = userId;
            var all = await _reservationRepo.GetByUserIdAsync(presaleUserId, ct);
            foreach (var r in all)
            {
                await _deferredScheduler.CancelAsync(SoftReservationExpiredJob.JobTaskName, r.Id?.Value.ToString() ?? "", ct);
                _cache.Remove(CacheKey(r.ProductId.Value, userId));

                if (_productUserIndex.TryGetValue(r.ProductId.Value, out var set))
                    lock (set) { set.Remove(userId); }
            }

            await _reservationRepo.DeleteAllForUserAsync(presaleUserId, ct);
        }

        private static string CacheKey(int productId, string userId) => $"sr:{productId}:{userId}";
    }
}
