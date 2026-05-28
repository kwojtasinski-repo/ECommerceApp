using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace RagTools.Core.Config;

/// <summary>
/// Decorator that caches the merged <see cref="RagConfigPayload"/> per collection via
/// <see cref="IDistributedCache"/>.
///
/// Why IDistributedCache: the default registration uses
/// <c>services.AddDistributedMemoryCache()</c> (in-process) so there is no infrastructure
/// dependency today. Phase 4 (multi-replica scaling) swaps in
/// <c>services.AddStackExchangeRedisCache(...)</c> with no application-code change.
///
/// Cache key:   <c>rag:config:{collection}</c>
/// Value:       JSON-serialized <see cref="RagConfigPayload"/>
/// Expiration:  5 min sliding (refreshed on Get) + 30 min absolute (hard ceiling)
///
/// Invalidation: <see cref="Invalidate"/> removes the entry — called by batch ingest after
/// <see cref="IDocumentStore.StoreConfigAsync"/> succeeds.
///
/// This is a thin layer over the existing <see cref="CachedDocumentStore"/> caching: that
/// caches the raw payload returned by Qdrant; this caches the merged effective config so
/// repeated queries skip the merge step too.
/// </summary>
public sealed class CachingConfigSource(IConfigSource inner, IDistributedCache cache) : IConfigSource
{
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan AbsoluteExpiration = TimeSpan.FromMinutes(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async ValueTask<RagConfigPayload> GetEffectiveAsync(string collection, CancellationToken ct = default)
    {
        var key = CacheKey(collection);
        var cached = await cache.GetAsync(key, ct);
        if (cached is not null && cached.Length > 0)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<RagConfigPayload>(cached, JsonOptions);
                if (payload is not null)
                {
                    // Refresh sliding TTL on hit.
                    await cache.RefreshAsync(key, ct);
                    return payload;
                }
            }
            catch (JsonException)
            {
                // Corrupted cache entry — fall through, refetch, overwrite.
                await cache.RemoveAsync(key, ct);
            }
        }

        var fresh = await inner.GetEffectiveAsync(collection, ct);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(fresh, JsonOptions);
        await cache.SetAsync(key, bytes, new DistributedCacheEntryOptions
        {
            SlidingExpiration = SlidingExpiration,
            AbsoluteExpirationRelativeToNow = AbsoluteExpiration,
        }, ct);
        return fresh;
    }

    public void Invalidate(string collection)
    {
        cache.Remove(CacheKey(collection));
        inner.Invalidate(collection);
    }

    private static string CacheKey(string collection) => $"rag:config:{collection}";
}
