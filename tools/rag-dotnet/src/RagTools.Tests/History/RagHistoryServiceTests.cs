using Microsoft.Extensions.Logging.Abstractions;
using RagTools.Core;
using RagTools.Core.Config;
using RagTools.Core.History;

namespace RagTools.Tests.History;

public class RagHistoryServiceTests
{
    private sealed class FakeEmbedder : IEmbedder
    {
        public Func<string, CancellationToken, Task<float[]>>? Handler { get; set; }
        public string? LastInput { get; private set; }
        public int Dimensions => 1;
        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            LastInput = text;
            return Handler!(text, ct);
        }
        public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
            => throw new NotImplementedException();
        public void Dispose() { }
    }

    private sealed class FakeStore : IDocumentStore
    {
        public Func<string, float[], SearchOptions, CancellationToken, Task<IReadOnlyList<DocumentSearchResult>>>? SearchHandler { get; set; }
        public Func<string, CancellationToken, Task<RagConfigPayload?>>? FetchConfigHandler { get; set; }
        public SearchOptions? LastSearchOptions { get; private set; }

        public Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(
            string collection, float[] queryVector, SearchOptions opts, CancellationToken ct = default)
        {
            LastSearchOptions = opts;
            return SearchHandler!(collection, queryVector, opts, ct);
        }
        public Task<RagConfigPayload?> FetchConfigAsync(string collection, CancellationToken ct = default)
            => FetchConfigHandler is null
                ? Task.FromResult<RagConfigPayload?>(null)
                : FetchConfigHandler(collection, ct);

        public Task UpsertChunksAsync(string collection, IReadOnlyList<RagPoint> chunks, CancellationToken ct = default) => throw new NotImplementedException();
        public Task StoreDocumentAsync(string collection, ContentDocument doc, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ContentDocument?> FetchContentAsync(string collection, string relPath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AdrSummary>> ListAdrsAsync(string collection, CancellationToken ct = default) => throw new NotImplementedException();
        public Task EnsureCollectionAsync(int dimensions, CancellationToken ct = default) => throw new NotImplementedException();
        public Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default) => throw new NotImplementedException();
        public Task RecreateCollectionAsync(string collection, int dimensions, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeletePointsForPathAsync(string collection, string relPath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteByPathsAsync(string collection, IEnumerable<string> relPaths, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteCollectionAsync(string collection, CancellationToken ct = default) => throw new NotImplementedException();
        public Task StoreConfigAsync(string collection, RagConfigPayload payload, CancellationToken ct = default) => throw new NotImplementedException();
        public void Dispose() { }
    }

    private sealed class FakeConfigSource : IConfigSource
    {
        public RagConfigPayload Payload { get; set; } = new();
        public Func<string, CancellationToken, ValueTask<RagConfigPayload>>? Handler { get; set; }
        public ValueTask<RagConfigPayload> GetEffectiveAsync(string collection, CancellationToken ct = default)
            => Handler is null ? ValueTask.FromResult(Payload) : Handler(collection, ct);
        public void Invalidate(string collection) { }
    }

    private static DocumentSearchResult Hit(string relPath, int startLine, string breadcrumb = "", string docKind = "doc", string text = "txt") =>
        new(0.5f, relPath, "", docKind, AdrId: null, breadcrumb, StartLine: startLine, EndLine: startLine, Text: text);

    private static RagHistoryService Build(out FakeEmbedder embedder, out FakeStore store, out FakeConfigSource config)
    {
        embedder = new FakeEmbedder();
        store = new FakeStore();
        config = new FakeConfigSource();
        return new RagHistoryService(embedder, store, config, NullLogger<RagHistoryService>.Instance);
    }

    private static RagHistoryService Build(out FakeEmbedder embedder, out FakeStore store)
        => Build(out embedder, out store, out _);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyId_ReturnsFailure_EmptyId(string id)
    {
        var sut = Build(out _, out _);
        var outcome = await sut.GetAsync(new HistoryRequest("c", id));
        var failure = Assert.IsType<HistoryOutcome.Failure>(outcome);
        Assert.Equal(HistoryError.EmptyId, failure.Error);
    }

    [Fact]
    public async Task EmbedderThrows_ReturnsFailure_EmbeddingFailed()
    {
        var sut = Build(out var embedder, out _);
        embedder.Handler = (_, _) => throw new InvalidOperationException("boom");
        var outcome = await sut.GetAsync(new HistoryRequest("c", "0016"));
        var failure = Assert.IsType<HistoryOutcome.Failure>(outcome);
        Assert.Equal(HistoryError.EmbeddingFailed, failure.Error);
    }

    [Fact]
    public async Task StoreThrows_ReturnsFailure_StoreSearchFailed_WithCollection()
    {
        var sut = Build(out var embedder, out var store);
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        store.SearchHandler = (_, _, _, _) => throw new InvalidOperationException("nope");
        var outcome = await sut.GetAsync(new HistoryRequest("col", "0016"));
        var failure = Assert.IsType<HistoryOutcome.Failure>(outcome);
        Assert.Equal(HistoryError.StoreSearchFailed, failure.Error);
        Assert.Equal("col", failure.Details!["collection"]);
    }

    [Fact]
    public async Task UsesConfiguredHistoryField_WhenPresent()
    {
        var sut = Build(out var embedder, out var store, out var config);
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        config.Payload = new RagConfigPayload { HistoryField = "rfc_id" };
        store.SearchHandler = (_, _, _, _) => Task.FromResult<IReadOnlyList<DocumentSearchResult>>(Array.Empty<DocumentSearchResult>());

        var outcome = await sut.GetAsync(new HistoryRequest("c", "RFC-003"));
        var success = Assert.IsType<HistoryOutcome.Success>(outcome);
        Assert.Equal("rfc_id", success.Response.HistoryField);
        Assert.Equal(("rfc_id", "RFC-003"), store.LastSearchOptions!.HistoryFieldFilter);
    }

    [Fact]
    public async Task FallsBackToDefaultField_WhenConfigMissing()
    {
        var sut = Build(out var embedder, out var store, out var config);
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        config.Payload = new RagConfigPayload { HistoryField = "" };
        store.SearchHandler = (_, _, _, _) => Task.FromResult<IReadOnlyList<DocumentSearchResult>>(Array.Empty<DocumentSearchResult>());

        var outcome = await sut.GetAsync(new HistoryRequest("c", "0016"));
        var success = Assert.IsType<HistoryOutcome.Success>(outcome);
        Assert.Equal("adr_id", success.Response.HistoryField);
    }

    [Fact]
    public async Task FallsBackToDefaultField_WhenFetchConfigThrows()
    {
        var sut = Build(out var embedder, out var store, out var config);
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        config.Handler = (_, _) => throw new InvalidOperationException("config gone");
        store.SearchHandler = (_, _, _, _) => Task.FromResult<IReadOnlyList<DocumentSearchResult>>(Array.Empty<DocumentSearchResult>());

        var outcome = await sut.GetAsync(new HistoryRequest("c", "0016"));
        var success = Assert.IsType<HistoryOutcome.Success>(outcome);
        Assert.Equal("adr_id", success.Response.HistoryField);
    }

    [Fact]
    public async Task EmbedsHistoryPrefixedString()
    {
        var sut = Build(out var embedder, out var store);
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        store.SearchHandler = (_, _, _, _) => Task.FromResult<IReadOnlyList<DocumentSearchResult>>(Array.Empty<DocumentSearchResult>());

        await sut.GetAsync(new HistoryRequest("c", "0016"));
        Assert.Equal("history 0016", embedder.LastInput);
    }

    [Fact]
    public async Task SearchOptions_TopK50_NoScoreThreshold()
    {
        var sut = Build(out var embedder, out var store);
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        store.SearchHandler = (_, _, _, _) => Task.FromResult<IReadOnlyList<DocumentSearchResult>>(Array.Empty<DocumentSearchResult>());

        await sut.GetAsync(new HistoryRequest("c", "0016"));
        Assert.Equal(50, store.LastSearchOptions!.TopK);
        Assert.Equal(0f, store.LastSearchOptions!.ScoreThreshold);
    }

    [Fact]
    public async Task Success_OrdersChunksByStartLine()
    {
        var sut = Build(out var embedder, out var store);
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        store.SearchHandler = (_, _, _, _) => Task.FromResult<IReadOnlyList<DocumentSearchResult>>(new[]
        {
            Hit("a.md", startLine: 30),
            Hit("a.md", startLine: 5),
            Hit("a.md", startLine: 20),
        });

        var outcome = await sut.GetAsync(new HistoryRequest("c", "0016"));
        var success = Assert.IsType<HistoryOutcome.Success>(outcome);
        Assert.Equal(new[] { 5, 20, 30 }, success.Response.Chunks.Select(c => c.StartLine).ToArray());
        Assert.Equal("0016", success.Response.Id);
    }
}
