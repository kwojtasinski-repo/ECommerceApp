using Microsoft.Extensions.Logging.Abstractions;
using RagTools.Core;
using RagTools.Core.Query;

namespace RagTools.Tests.Query;

public class RagQueryServiceTests
{
    private sealed class FakeEmbedder : IEmbedder
    {
        public Func<string, CancellationToken, Task<float[]>>? Handler { get; set; }
        public int Dimensions => 1;
        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => Handler!(text, ct);
        public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
            => throw new NotImplementedException();
        public void Dispose() { }
    }

    private sealed class FakeStore : IDocumentStore
    {
        public Func<string, float[], SearchOptions, CancellationToken, Task<IReadOnlyList<DocumentSearchResult>>>? SearchHandler { get; set; }
        public SearchOptions? LastSearchOptions { get; private set; }

        public Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(
            string collection, float[] queryVector, SearchOptions opts, CancellationToken ct = default)
        {
            LastSearchOptions = opts;
            return SearchHandler!(collection, queryVector, opts, ct);
        }

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
        public Task<RagConfigPayload?> FetchConfigAsync(string collection, CancellationToken ct = default) => throw new NotImplementedException();
        public void Dispose() { }
    }

    private sealed class FakePostprocessor : IResultPostprocessor
    {
        public Func<IReadOnlyList<DocumentSearchResult>, QueryContext, CancellationToken, Task<IReadOnlyList<DocumentSearchResult>>>? Handler { get; set; }
        public Task<IReadOnlyList<DocumentSearchResult>> ProcessAsync(
            IReadOnlyList<DocumentSearchResult> hits, QueryContext ctx, CancellationToken ct = default)
            => Handler!(hits, ctx, ct);
    }

    private static DocumentSearchResult Hit(
        float score, string relPath, string breadcrumb = "", string docTitle = "", string docKind = "doc") =>
        new(score, relPath, docTitle, docKind, AdrId: null, breadcrumb, StartLine: 1, EndLine: 1, Text: "txt");

    private static RagQueryService Build(
        out FakeEmbedder embedder,
        out FakeStore store,
        IEnumerable<IResultPostprocessor>? postprocessors = null,
        RagConfig? cfg = null)
    {
        embedder = new FakeEmbedder();
        store = new FakeStore();
        return new RagQueryService(
            embedder, store, cfg ?? new RagConfig(),
            postprocessors ?? Array.Empty<IResultPostprocessor>(),
            NullLogger<RagQueryService>.Instance);
    }

    [Fact]
    public async Task EmptyQuestion_ReturnsFailure_EmptyQuestion()
    {
        var sut = Build(out _, out _);
        var outcome = await sut.QueryAsync(new QueryRequest("c", "   "));
        var failure = Assert.IsType<QueryOutcome.Failure>(outcome);
        Assert.Equal(QueryError.EmptyQuestion, failure.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    [InlineData(100)]
    public async Task TopKOutOfRange_ReturnsFailure_WithDetails(int topK)
    {
        var sut = Build(out _, out _);
        var outcome = await sut.QueryAsync(new QueryRequest("c", "q", TopK: topK));
        var failure = Assert.IsType<QueryOutcome.Failure>(outcome);
        Assert.Equal(QueryError.TopKOutOfRange, failure.Error);
        Assert.NotNull(failure.Details);
        Assert.Equal(topK, failure.Details!["topK"]);
        Assert.Equal(RagQueryService.MaxTopK, failure.Details!["max"]);
    }

    [Fact]
    public async Task EmbedderThrows_ReturnsFailure_EmbeddingFailed()
    {
        var sut = Build(out var embedder, out _);
        embedder.Handler = (_, _) => throw new InvalidOperationException("boom");
        var outcome = await sut.QueryAsync(new QueryRequest("c", "q"));
        var failure = Assert.IsType<QueryOutcome.Failure>(outcome);
        Assert.Equal(QueryError.EmbeddingFailed, failure.Error);
        Assert.Contains("boom", failure.Message);
    }

    [Fact]
    public async Task StoreThrows_ReturnsFailure_StoreSearchFailed_WithCollection()
    {
        var sut = Build(out var embedder, out var store);
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        store.SearchHandler = (_, _, _, _) => throw new InvalidOperationException("nope");
        var outcome = await sut.QueryAsync(new QueryRequest("col", "q"));
        var failure = Assert.IsType<QueryOutcome.Failure>(outcome);
        Assert.Equal(QueryError.StoreSearchFailed, failure.Error);
        Assert.Equal("col", failure.Details!["collection"]);
    }

    [Fact]
    public async Task PostprocessorThrows_ReturnsFailure_PostprocessorFailed()
    {
        var pp = new FakePostprocessor { Handler = (_, _, _) => throw new InvalidOperationException("pp fail") };
        var sut = Build(out var embedder, out var store, new[] { pp });
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        store.SearchHandler = (_, _, _, _) =>
            Task.FromResult<IReadOnlyList<DocumentSearchResult>>(new[] { Hit(0.9f, "a.md") });
        var outcome = await sut.QueryAsync(new QueryRequest("c", "q"));
        var failure = Assert.IsType<QueryOutcome.Failure>(outcome);
        Assert.Equal(QueryError.PostprocessorFailed, failure.Error);
        Assert.Contains("pp fail", failure.Message);
    }

    [Fact]
    public async Task TopicFilter_WidensFetchK_ToTopKTimes3()
    {
        var sut = Build(out var embedder, out var store);
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        store.SearchHandler = (_, _, _, _) =>
            Task.FromResult<IReadOnlyList<DocumentSearchResult>>(Array.Empty<DocumentSearchResult>());
        await sut.QueryAsync(new QueryRequest("c", "q", Topic: "Orders", TopK: 10));
        Assert.Equal(30, store.LastSearchOptions!.TopK);
    }

    [Fact]
    public async Task NoTopicFilter_UsesConfigFetchK()
    {
        var sut = Build(out var embedder, out var store);
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        store.SearchHandler = (_, _, _, _) =>
            Task.FromResult<IReadOnlyList<DocumentSearchResult>>(Array.Empty<DocumentSearchResult>());
        await sut.QueryAsync(new QueryRequest("c", "q", TopK: 5));
        Assert.Equal(20, store.LastSearchOptions!.TopK);
    }

    [Fact]
    public async Task Success_RanksAndProjectsHits()
    {
        var sut = Build(out var embedder, out var store);
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        var raw = new DocumentSearchResult[]
        {
            Hit(0.5f, "a.md", "Catalog"),
            Hit(0.9f, "b.md", "Orders"),
            Hit(0.7f, "c.md", "Sales"),
        };
        store.SearchHandler = (_, _, _, _) => Task.FromResult<IReadOnlyList<DocumentSearchResult>>(raw);
        var outcome = await sut.QueryAsync(new QueryRequest("c", "q", TopK: 2));
        var success = Assert.IsType<QueryOutcome.Success>(outcome);
        Assert.Equal(3, success.Response.TotalCandidates);
        Assert.Equal(2, success.Response.Hits.Count);
        Assert.Equal(1, success.Response.Hits[0].Rank);
        Assert.Equal("b.md", success.Response.Hits[0].RelPath);
        Assert.Equal(2, success.Response.Hits[1].Rank);
        Assert.Equal("c.md", success.Response.Hits[1].RelPath);
    }

    [Fact]
    public async Task TopicFilter_FiltersByBreadcrumbOrDocTitle_CaseInsensitive()
    {
        var sut = Build(out var embedder, out var store);
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        var raw = new DocumentSearchResult[]
        {
            Hit(0.9f, "a.md", breadcrumb: "Catalog > X"),
            Hit(0.8f, "b.md", breadcrumb: "ORDERS > Y"),
            Hit(0.7f, "c.md", docTitle: "orders overview"),
        };
        store.SearchHandler = (_, _, _, _) => Task.FromResult<IReadOnlyList<DocumentSearchResult>>(raw);
        var outcome = await sut.QueryAsync(new QueryRequest("c", "q", Topic: "Orders", TopK: 10));
        var success = Assert.IsType<QueryOutcome.Success>(outcome);
        Assert.Equal(2, success.Response.Hits.Count);
        Assert.Equal("b.md", success.Response.Hits[0].RelPath);
        Assert.Equal("c.md", success.Response.Hits[1].RelPath);
    }
}
