using System.Collections.Concurrent;

namespace RagTools.Core;

/// <summary>
/// Lightweight in-memory cache for RAG query results.
///
/// Design constraints:
///   - No external dependencies (no Redis, MemoryCache, etc.)
///   - Thread-safe for concurrent MCP tool calls
///   - Bounded size: evicts oldest entries when capacity is reached
///   - Per-entry TTL: stale results are never returned
///
/// Used by <see cref="CachedDocumentStore"/> to wrap any <see cref="IDocumentStore"/>.
/// </summary>
public sealed class QueryCache
{
    private sealed record CacheEntry(object Value, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly int _maxEntries;
    private readonly TimeSpan _defaultTtl;

    // Track insertion order for LRU-ish eviction (approximate — no full LRU overhead).
    private readonly ConcurrentQueue<string> _insertionOrder = new();

    public QueryCache(int maxEntries = 512, TimeSpan? defaultTtl = null)
    {
        _maxEntries  = maxEntries;
        _defaultTtl  = defaultTtl ?? TimeSpan.FromMinutes(5);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Get a cached value or compute it, caching the result.</summary>
    public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null)
    {
        if (_cache.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
            return (T)entry.Value;

        var value = await factory();
        Set(key, value!, ttl);
        return value;
    }

    /// <summary>Invalidate all entries whose key starts with the given prefix.</summary>
    public void InvalidatePrefix(string prefix)
    {
        foreach (var key in _cache.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
            _cache.TryRemove(key, out _);
    }

    /// <summary>Remove all cached entries.</summary>
    public void Clear() => _cache.Clear();

    /// <summary>Current number of live (non-expired) entries.</summary>
    public int Count => _cache.Count(kv => kv.Value.ExpiresAt > DateTimeOffset.UtcNow);

    // ── Internal ──────────────────────────────────────────────────────────────

    private void Set(string key, object value, TimeSpan? ttl)
    {
        // Evict oldest entries if at capacity (approximate FIFO, not true LRU).
        while (_cache.Count >= _maxEntries && _insertionOrder.TryDequeue(out var oldest))
            _cache.TryRemove(oldest, out _);

        var entry = new CacheEntry(value, DateTimeOffset.UtcNow + (ttl ?? _defaultTtl));
        _cache[key] = entry;
        _insertionOrder.Enqueue(key);
    }
}
