using Microsoft.Extensions.Caching.Distributed;
using RagTools.Core;
using RagTools.Core.Config;

namespace RagTools.Tests.Config;

/// <summary>
/// Unit tests for <see cref="CachingConfigSource"/>. Uses a hand-written
/// in-memory <see cref="IDistributedCache"/> so the test has no NuGet dependency
/// on Microsoft.Extensions.Caching.Memory.
/// </summary>
public sealed class CachingConfigSourceTests
{
    private sealed class DictDistributedCache : IDistributedCache
    {
        public Dictionary<string, byte[]> Store { get; } = new();
        public int RemoveCount { get; private set; }

        public byte[]? Get(string key) => Store.TryGetValue(key, out var v) ? v : null;
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) { RemoveCount++; Store.Remove(key); }
        public Task RemoveAsync(string key, CancellationToken token = default) { Remove(key); return Task.CompletedTask; }
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => Store[key] = value;
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }
    }

    private sealed class CountingInner : IConfigSource
    {
        public int GetCount { get; private set; }
        public int InvalidateCount { get; private set; }
        public RagConfigPayload Payload { get; set; } = new() { FetchK = 42, ScoreThreshold = 0.5f };

        public ValueTask<RagConfigPayload> GetEffectiveAsync(string collection, CancellationToken ct = default)
        {
            GetCount++;
            return new(Payload);
        }
        public void Invalidate(string collection) => InvalidateCount++;
    }

    [Fact]
    public async Task FirstCall_HitsInner_AndCachesResult()
    {
        var inner = new CountingInner();
        var cache = new DictDistributedCache();
        var sut = new CachingConfigSource(inner, cache);

        var result = await sut.GetEffectiveAsync("col-a");

        Assert.Equal(42, result.FetchK);
        Assert.Equal(1, inner.GetCount);
        Assert.True(cache.Store.ContainsKey("rag:config:col-a"));
    }

    [Fact]
    public async Task SecondCall_ServesFromCache_DoesNotHitInner()
    {
        var inner = new CountingInner();
        var sut = new CachingConfigSource(inner, new DictDistributedCache());

        await sut.GetEffectiveAsync("col-a");
        await sut.GetEffectiveAsync("col-a");

        Assert.Equal(1, inner.GetCount);
    }

    [Fact]
    public async Task DifferentCollections_AreCachedSeparately()
    {
        var inner = new CountingInner();
        var sut = new CachingConfigSource(inner, new DictDistributedCache());

        await sut.GetEffectiveAsync("col-a");
        await sut.GetEffectiveAsync("col-b");
        await sut.GetEffectiveAsync("col-a");
        await sut.GetEffectiveAsync("col-b");

        Assert.Equal(2, inner.GetCount); // one per collection
    }

    [Fact]
    public async Task Invalidate_RemovesCacheEntry_AndCallsInner()
    {
        var inner = new CountingInner();
        var cache = new DictDistributedCache();
        var sut = new CachingConfigSource(inner, cache);

        await sut.GetEffectiveAsync("col-a");
        Assert.True(cache.Store.ContainsKey("rag:config:col-a"));

        sut.Invalidate("col-a");

        Assert.False(cache.Store.ContainsKey("rag:config:col-a"));
        Assert.Equal(1, inner.InvalidateCount);

        // Next call must hit inner again.
        await sut.GetEffectiveAsync("col-a");
        Assert.Equal(2, inner.GetCount);
    }

    [Fact]
    public async Task CorruptedCacheEntry_IsRecoveredViaRefetch()
    {
        var inner = new CountingInner();
        var cache = new DictDistributedCache();
        cache.Store["rag:config:col-a"] = [0xFF, 0xFE, 0x00]; // invalid JSON
        var sut = new CachingConfigSource(inner, cache);

        var result = await sut.GetEffectiveAsync("col-a");

        Assert.Equal(42, result.FetchK);
        Assert.Equal(1, inner.GetCount);
        // Corrupted entry replaced with valid JSON.
        Assert.True(cache.Store["rag:config:col-a"].Length > 3);
    }
}
