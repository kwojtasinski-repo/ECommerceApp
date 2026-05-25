using Microsoft.Extensions.Logging.Abstractions;
using RagTools.Core;
using RagTools.Core.Adrs;

namespace RagTools.Tests.Adrs;

public class RagListServiceTests
{
    private sealed class FakeStore : IDocumentStore
    {
        public Func<string, CancellationToken, Task<IReadOnlyList<AdrSummary>>>? Handler { get; set; }
        public Task<IReadOnlyList<AdrSummary>> ListAdrsAsync(string collection, CancellationToken ct = default)
            => Handler!(collection, ct);

        public Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(string c, float[] v, SearchOptions o, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpsertChunksAsync(string collection, IReadOnlyList<RagPoint> chunks, CancellationToken ct = default) => throw new NotImplementedException();
        public Task StoreDocumentAsync(string collection, ContentDocument doc, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ContentDocument?> FetchContentAsync(string collection, string relPath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task EnsureCollectionAsync(int dimensions, CancellationToken ct = default) => throw new NotImplementedException();
        public Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default) => throw new NotImplementedException();
        public Task RecreateCollectionAsync(string collection, int dimensions, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeletePointsForPathAsync(string collection, string relPath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteByPathsAsync(string collection, IEnumerable<string> relPaths, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteCollectionAsync(string collection, CancellationToken ct = default) => throw new NotImplementedException();
        public Task StoreConfigAsync(string collection, RagConfigPayload payload, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<RagConfigPayload?> FetchConfigAsync(string collection, CancellationToken ct = default) => throw new NotImplementedException();
        public void Dispose() { }
    }

    private static RagListService Build(out FakeStore store)
    {
        store = new FakeStore();
        return new RagListService(store, NullLogger<RagListService>.Instance);
    }

    [Fact]
    public async Task Success_ProjectsSummariesFromStore()
    {
        var sut = Build(out var store);
        var data = new AdrSummary[]
        {
            new("0001", "First", "docs/adr/0001/0001.md", 0, 1),
            new("0016", "Coupons", "docs/adr/0016/0016.md", 2, 0),
        };
        store.Handler = (_, _) => Task.FromResult<IReadOnlyList<AdrSummary>>(data);

        var outcome = await sut.ListAsync(new AdrListRequest("c"));
        var success = Assert.IsType<AdrListOutcome.Success>(outcome);
        Assert.Equal(2, success.Response.Adrs.Count);
        Assert.Equal("0016", success.Response.Adrs[1].Id);
        Assert.Equal(2, success.Response.Adrs[1].Amendments);
    }

    [Fact]
    public async Task EmptyList_Succeeds()
    {
        var sut = Build(out var store);
        store.Handler = (_, _) => Task.FromResult<IReadOnlyList<AdrSummary>>(Array.Empty<AdrSummary>());
        var outcome = await sut.ListAsync(new AdrListRequest("c"));
        var success = Assert.IsType<AdrListOutcome.Success>(outcome);
        Assert.Empty(success.Response.Adrs);
    }

    [Fact]
    public async Task StoreThrows_ReturnsFailure_StoreFetchFailed_WithCollection()
    {
        var sut = Build(out var store);
        store.Handler = (_, _) => throw new InvalidOperationException("qdrant down");
        var outcome = await sut.ListAsync(new AdrListRequest("col"));
        var failure = Assert.IsType<AdrListOutcome.Failure>(outcome);
        Assert.Equal(AdrListError.StoreFetchFailed, failure.Error);
        Assert.Equal("col", failure.Details!["collection"]);
        Assert.Contains("qdrant down", failure.Message);
    }

    [Fact]
    public async Task PassesCollectionThroughToStore()
    {
        var sut = Build(out var store);
        string? seen = null;
        store.Handler = (c, _) => { seen = c; return Task.FromResult<IReadOnlyList<AdrSummary>>(Array.Empty<AdrSummary>()); };
        await sut.ListAsync(new AdrListRequest("my-coll"));
        Assert.Equal("my-coll", seen);
    }

    [Fact]
    public async Task CancellationFlows_DoesNotSwallow()
    {
        var sut = Build(out var store);
        store.Handler = (_, ct) => Task.FromException<IReadOnlyList<AdrSummary>>(new OperationCanceledException());
        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.ListAsync(new AdrListRequest("c")));
    }
}
