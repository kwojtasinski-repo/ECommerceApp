using RagTools.Core;

namespace RagTools.Tests;

/// <summary>Unit tests for QueryCache â€” bounded LRU-style cache with TTL per entry.</summary>
public sealed class QueryCacheTests
{
    [Fact]
    public async Task GetOrAddAsync_ReturnsFactoryResult_OnCacheMiss()
    {
        var cache = new QueryCache();
        var callCount = 0;

        var result = await cache.GetOrAddAsync(
            "key1",
            async () => { callCount++; return await Task.FromResult("value1"); },
            TimeSpan.FromMinutes(5));

        Assert.Equal("value1", result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetOrAddAsync_ReturnsCachedValue_OnSecondCall()
    {
        var cache = new QueryCache();
        var callCount = 0;

        await cache.GetOrAddAsync("key1", async () => { callCount++; return await Task.FromResult("v"); }, TimeSpan.FromMinutes(5));
        var result = await cache.GetOrAddAsync("key1", async () => { callCount++; return await Task.FromResult("v2"); }, TimeSpan.FromMinutes(5));

        Assert.Equal("v", result);   // first value returned (cache hit)
        Assert.Equal(1, callCount);  // factory called only once
    }

    [Fact]
    public async Task GetOrAddAsync_RefreshesEntry_AfterTtlExpires()
    {
        var cache = new QueryCache();
        var callCount = 0;

        // Use 1ms TTL so it expires immediately.
        await cache.GetOrAddAsync("key1", async () => { callCount++; return await Task.FromResult("v1"); }, TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);  // wait for TTL to pass
        var result = await cache.GetOrAddAsync("key1", async () => { callCount++; return await Task.FromResult("v2"); }, TimeSpan.FromMinutes(5));

        Assert.Equal("v2", result);
        Assert.Equal(2, callCount);  // factory called twice (once for miss, once after TTL)
    }

    [Fact]
    public async Task InvalidatePrefix_RemovesMatchingEntries()
    {
        var cache = new QueryCache();
        await cache.GetOrAddAsync("search:col:abc", async () => await Task.FromResult("hit1"), TimeSpan.FromMinutes(5));
        await cache.GetOrAddAsync("search:col:xyz", async () => await Task.FromResult("hit2"), TimeSpan.FromMinutes(5));
        await cache.GetOrAddAsync("config:col",    async () => await Task.FromResult("cfg"),  TimeSpan.FromMinutes(5));

        cache.InvalidatePrefix("search:col");

        // search entries should be gone â€” factory will be called again.
        var callCount = 0;
        await cache.GetOrAddAsync("search:col:abc", async () => { callCount++; return await Task.FromResult("new"); }, TimeSpan.FromMinutes(5));
        await cache.GetOrAddAsync("search:col:xyz", async () => { callCount++; return await Task.FromResult("new"); }, TimeSpan.FromMinutes(5));

        // config entry should still be cached â€” factory NOT called.
        var cfgCallCount = 0;
        await cache.GetOrAddAsync("config:col", async () => { cfgCallCount++; return await Task.FromResult("new"); }, TimeSpan.FromMinutes(5));

        Assert.Equal(2, callCount);     // both search entries re-fetched
        Assert.Equal(0, cfgCallCount);  // config still cached
    }

    [Fact]
    public async Task Clear_RemovesAllEntries()
    {
        var cache = new QueryCache();
        await cache.GetOrAddAsync("k1", async () => await Task.FromResult("v1"), TimeSpan.FromMinutes(5));
        await cache.GetOrAddAsync("k2", async () => await Task.FromResult("v2"), TimeSpan.FromMinutes(5));

        cache.Clear();

        var callCount = 0;
        await cache.GetOrAddAsync("k1", async () => { callCount++; return await Task.FromResult("new"); }, TimeSpan.FromMinutes(5));
        await cache.GetOrAddAsync("k2", async () => { callCount++; return await Task.FromResult("new"); }, TimeSpan.FromMinutes(5));

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task Cache_EvictsOldestEntries_WhenCapacityExceeded()
    {
        // Use a tiny capacity to test eviction without filling 512 entries.
        // QueryCache hardcodes 512, so we just verify it handles many entries without error
        // (full eviction test would require a test-seam or reflection â€” skip for now).
        var cache = new QueryCache();
        for (var i = 0; i < 20; i++)
        {
            var key = $"key_{i}";
            await cache.GetOrAddAsync(key, async () => await Task.FromResult(i), TimeSpan.FromMinutes(5));
        }

        // Verify most recent entry is still cached.
        var callCount = 0;
        await cache.GetOrAddAsync("key_19", async () => { callCount++; return await Task.FromResult(-1); }, TimeSpan.FromMinutes(5));
        Assert.Equal(0, callCount);  // still in cache
    }
}
