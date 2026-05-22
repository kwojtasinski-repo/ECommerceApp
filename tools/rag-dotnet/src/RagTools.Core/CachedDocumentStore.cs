using System.Security.Cryptography;

namespace RagTools.Core;

/// <summary>
/// Decorator that wraps any <see cref="IDocumentStore"/> with a <see cref="QueryCache"/>.
///
/// Cache strategy:
///   - Read operations (Search, FetchContent, FetchConfig, ListAdrs) — cached with TTL.
///   - Write operations (UpsertChunks, StoreDocument, StoreConfig, DeleteByPaths)
///     — pass through + invalidate relevant cache entries so reads never serve stale data.
///
/// Cache key scheme (prefix-based for easy invalidation):
///   search:{collection}:{hash}         — SearchAsync results
///   content:{collection}:{relPath}     — FetchContentAsync results
///   config:{collection}                — FetchConfigAsync results
///   adrs:{collection}                  — ListAdrsAsync results
/// </summary>
public sealed class CachedDocumentStore(IDocumentStore inner, QueryCache cache) : IDocumentStore
{
    // ── Write-through operations (invalidate on change) ───────────────────────

    public async Task UpsertChunksAsync(string collection, IReadOnlyList<RagPoint> chunks, CancellationToken ct = default)
    {
        await inner.UpsertChunksAsync(collection, chunks, ct);
        // Chunks changed — search results are stale.
        cache.InvalidatePrefix($"search:{collection}:");
    }

    public async Task StoreDocumentAsync(string collection, ContentDocument doc, CancellationToken ct = default)
    {
        await inner.StoreDocumentAsync(collection, doc, ct);
        // Invalidate the specific content entry.
        cache.InvalidatePrefix($"content:{collection}:{doc.RelPath}");
    }

    public async Task StoreConfigAsync(string collection, RagConfigPayload config, CancellationToken ct = default)
    {
        await inner.StoreConfigAsync(collection, config, ct);
        cache.InvalidatePrefix($"config:{collection}");
    }

    public async Task DeleteByPathsAsync(string collection, IEnumerable<string> relPaths, CancellationToken ct = default)
    {
        var paths = relPaths.ToList();
        await inner.DeleteByPathsAsync(collection, paths, ct);
        // Invalidate content entries for each deleted path and all search results.
        cache.InvalidatePrefix($"search:{collection}:");
        foreach (var p in paths)
            cache.InvalidatePrefix($"content:{collection}:{p}");
    }

    // ── Cached read operations ────────────────────────────────────────────────

    public Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(
        string collection,
        float[] queryVector,
        SearchOptions opts,
        CancellationToken ct = default)
    {
        var key = $"search:{collection}:{VectorHash(queryVector)}:{opts.TopK}:{opts.ScoreThreshold}:{opts.DocKindFilter}:{opts.AdrIdFilter}";
        return cache.GetOrAddAsync(key, () => inner.SearchAsync(collection, queryVector, opts, ct));
    }

    public Task<ContentDocument?> FetchContentAsync(string collection, string relPath, CancellationToken ct = default)
    {
        var key = $"content:{collection}:{relPath}";
        // Longer TTL for content — files don't change often.
        return cache.GetOrAddAsync(key, () => inner.FetchContentAsync(collection, relPath, ct),
            ttl: TimeSpan.FromMinutes(15));
    }

    public Task<RagConfigPayload?> FetchConfigAsync(string collection, CancellationToken ct = default)
    {
        var key = $"config:{collection}";
        // Config rarely changes — cache for 30 minutes.
        return cache.GetOrAddAsync(key, () => inner.FetchConfigAsync(collection, ct),
            ttl: TimeSpan.FromMinutes(30));
    }

    public Task<IReadOnlyList<AdrSummary>> ListAdrsAsync(string collection, CancellationToken ct = default)
    {
        var key = $"adrs:{collection}";
        return cache.GetOrAddAsync(key, () => inner.ListAdrsAsync(collection, ct),
            ttl: TimeSpan.FromMinutes(10));
    }

    // ── Collection management (pass-through) ──────────────────────────────────

    public Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default) =>
        inner.EnsureCollectionAsync(collection, dimensions, ct);

    public Task RecreateCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        cache.Clear();  // full rebuild — clear everything
        return inner.RecreateCollectionAsync(collection, dimensions, ct);
    }

    public void Dispose() => inner.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Stable 8-char hex fingerprint of a float[] embedding vector.
    /// Used as the search cache key discriminator.
    /// Not cryptographic — just stable identity.
    /// </summary>
    private static string VectorHash(float[] vector)
    {
        var bytes = new byte[vector.Length * 4];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        var hash = MD5.HashData(bytes);
        return Convert.ToHexString(hash)[..8];  // first 4 bytes = 8 hex chars
    }
}
