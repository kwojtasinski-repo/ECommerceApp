using System;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Sales.Coupons.Contracts;
using ECommerceApp.Domain.Sales.Coupons;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECommerceApp.Infrastructure.Sales.Coupons
{
    internal sealed class SpecialEventCache : ISpecialEventCache
    {
        private readonly CouponsDbContext _context;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

        public SpecialEventCache(CouponsDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<SpecialEvent> GetByCodeAsync(string eventCode, CancellationToken ct = default)
        {
            var cacheKey = $"special-event:{eventCode}";

            if (_cache.TryGetValue(cacheKey, out SpecialEvent cached))
            {
                return cached;
            }

            var entity = await _context.SpecialEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Code == eventCode, ct);

            if (entity is not null)
            {
                _cache.Set(cacheKey, entity, CacheDuration);
            }

            return entity;
        }

        public void Invalidate()
        {
            if (_cache is MemoryCache mc)
            {
                mc.Compact(1.0);
            }
        }
    }
}
