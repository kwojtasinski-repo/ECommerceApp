using Microsoft.Extensions.Logging.Abstractions;
using RagTools.Core;
using RagTools.Core.ContentSources;
using RagTools.Core.ReadDocs;

namespace RagTools.Tests.ReadDocs;

public class RagReadDocsServiceTests
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
        public Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(
            string collection, float[] queryVector, SearchOptions opts, CancellationToken ct = default)
            => SearchHandler!(collection, queryVector, opts, ct);

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

    private sealed class FakeContentSource : IContentSource
    {
        public Func<string, string, CancellationToken, Task<string?>>? Handler { get; set; }
        public Task<string?> ReadAsync(string collection, string relPath, CancellationToken ct = default)
            => Handler!(collection, relPath, ct);
    }

    private static DocumentSearchResult Hit(
        float score, string relPath, int startLine = 1, string breadcrumb = "", string docTitle = "", string docKind = "doc", string text = "txt") =>
        new(score, relPath, docTitle, docKind, AdrId: null, breadcrumb, StartLine: startLine, Text: text);

    private static RagReadDocsService Build(
        out FakeEmbedder embedder, out FakeStore store, out FakeContentSource content, RagConfig? cfg = null)
    {
        embedder = new FakeEmbedder();
        store = new FakeStore();
        content = new FakeContentSource();
        return new RagReadDocsService(embedder, store, content, cfg ?? new RagConfig(), NullLogger<RagReadDocsService>.Instance);
    }

    [Fact]
    public async Task EmptyQuestion_ReturnsFailure_EmptyQuestion()
    {
        var sut = Build(out _, out _, out _);
        var outcome = await sut.ReadAsync(new ReadDocsRequest("c", "   "));
        var failure = Assert.IsType<ReadDocsOutcome.Failure>(outcome);
        Assert.Equal(ReadDocsError.EmptyQuestion, failure.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(100)]
    public async Task TopFilesOutOfRange_ReturnsFailure_WithDetails(int topFiles)
    {
        var sut = Build(out _, out _, out _);
        var outcome = await sut.ReadAsync(new ReadDocsRequest("c", "q", TopFiles: topFiles));
        var failure = Assert.IsType<ReadDocsOutcome.Failure>(outcome);
        Assert.Equal(ReadDocsError.TopFilesOutOfRange, failure.Error);
        Assert.Equal(topFiles, failure.Details!["topFiles"]);
        Assert.Equal(RagReadDocsService.MaxTopFiles, failure.Details!["max"]);
    }

    [Fact]
    public async Task EmbedderThrows_ReturnsFailure_EmbeddingFailed()
    {
        var sut = Build(out var embedder, out _, out _);
        embedder.Handler = (_, _) => throw new InvalidOperationException("boom");
        var outcome = await sut.ReadAsync(new ReadDocsRequest("c", "q"));
        var failure = Assert.IsType<ReadDocsOutcome.Failure>(outcome);
        Assert.Equal(ReadDocsError.EmbeddingFailed, failure.Error);
        Assert.Contains("boom", failure.Message);
    }

    [Fact]
    public async Task StoreThrows_ReturnsFailure_StoreSearchFailed_WithCollection()
    {
        var sut = Build(out var embedder, out var store, out _);
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        store.SearchHandler = (_, _, _, _) => throw new InvalidOperationException("nope");
        var outcome = await sut.ReadAsync(new ReadDocsRequest("col", "q"));
        var failure = Assert.IsType<ReadDocsOutcome.Failure>(outcome);
        Assert.Equal(ReadDocsError.StoreSearchFailed, failure.Error);
        Assert.Equal("col", failure.Details!["collection"]);
    }

    [Fact]
    public async Task ChunksMode_GroupsByRelPath_PicksMaxScore_AndOrdersByBest()
    {
        var sut = Build(out var embedder, out var store, out _);
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        store.SearchHandler = (_, _, _, _) => Task.FromResult<IReadOnlyList<DocumentSearchResult>>(new[]
        {
            Hit(0.5f, "a.md", startLine: 1),
            Hit(0.9f, "a.md", startLine: 10),
            Hit(0.7f, "b.md", startLine: 1),
        });

        var outcome = await sut.ReadAsync(new ReadDocsRequest("c", "q", TopFiles: 2));
        var success = Assert.IsType<ReadDocsOutcome.Success>(outcome);
        Assert.Equal(ReadDocsMode.Chunks, success.Response.Mode);
        Assert.Equal(2, success.Response.Files.Count);
        Assert.Equal("a.md", success.Response.Files[0].RelPath);
        Assert.Equal(0.9, success.Response.Files[0].Score);
        Assert.Null(success.Response.Files[0].Content);
        Assert.Equal(2, success.Response.Files[0].Chunks.Count);
        // Chunks ranked within file by score desc
        Assert.Equal(1, success.Response.Files[0].Chunks[0].Rank);
        Assert.Equal(0.9, success.Response.Files[0].Chunks[0].Score);
    }

    [Fact]
    public async Task FullMode_TriggeredByPhrase_FetchesContentSource()
    {
        var sut = Build(out var embedder, out var store, out var content);
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        store.SearchHandler = (_, _, _, _) => Task.FromResult<IReadOnlyList<DocumentSearchResult>>(new[] { Hit(0.9f, "a.md") });
        content.Handler = (_, _, _) => Task.FromResult<string?>("FULL BODY");

        var outcome = await sut.ReadAsync(new ReadDocsRequest("c", "show me all details about X", TopFiles: 1));
        var success = Assert.IsType<ReadDocsOutcome.Success>(outcome);
        Assert.Equal(ReadDocsMode.Full, success.Response.Mode);
        Assert.Equal("FULL BODY", success.Response.Files[0].Content);
        Assert.Empty(success.Response.Files[0].Chunks);
    }

    [Fact]
    public async Task FullMode_ContentNull_FallsBackToPlaceholder()
    {
        var sut = Build(out var embedder, out var store, out var content);
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        store.SearchHandler = (_, _, _, _) => Task.FromResult<IReadOnlyList<DocumentSearchResult>>(new[] { Hit(0.9f, "a.md") });
        content.Handler = (_, _, _) => Task.FromResult<string?>(null);

        var outcome = await sut.ReadAsync(new ReadDocsRequest("c", "full content of A", TopFiles: 1));
        var success = Assert.IsType<ReadDocsOutcome.Success>(outcome);
        Assert.Contains("[Content unavailable", success.Response.Files[0].Content);
    }

    [Fact]
    public async Task FullMode_ContentSourceThrows_ReturnsFailure_WithRelPath()
    {
        var sut = Build(out var embedder, out var store, out var content);
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        store.SearchHandler = (_, _, _, _) => Task.FromResult<IReadOnlyList<DocumentSearchResult>>(new[] { Hit(0.9f, "a.md") });
        content.Handler = (_, _, _) => throw new InvalidOperationException("disk fail");

        var outcome = await sut.ReadAsync(new ReadDocsRequest("c", "whole file please", TopFiles: 1));
        var failure = Assert.IsType<ReadDocsOutcome.Failure>(outcome);
        Assert.Equal(ReadDocsError.ContentFetchFailed, failure.Error);
        Assert.Equal("a.md", failure.Details!["relPath"]);
    }

    [Fact]
    public async Task ChunksMode_LimitedTo8PerFile()
    {
        var sut = Build(out var embedder, out var store, out _);
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        var raw = Enumerable.Range(0, 12).Select(i => Hit(0.9f - i * 0.01f, "a.md", startLine: i)).ToArray();
        store.SearchHandler = (_, _, _, _) => Task.FromResult<IReadOnlyList<DocumentSearchResult>>(raw);

        var outcome = await sut.ReadAsync(new ReadDocsRequest("c", "q", TopFiles: 1));
        var success = Assert.IsType<ReadDocsOutcome.Success>(outcome);
        Assert.Equal(8, success.Response.Files[0].Chunks.Count);
    }

    [Fact]
    public async Task TopicFilter_FiltersByBreadcrumbOrDocTitle_CaseInsensitive()
    {
        var sut = Build(out var embedder, out var store, out _);
        embedder.Handler = (_, _) => Task.FromResult(new float[] { 0.1f });
        store.SearchHandler = (_, _, _, _) => Task.FromResult<IReadOnlyList<DocumentSearchResult>>(new[]
        {
            Hit(0.9f, "a.md", breadcrumb: "Catalog > X"),
            Hit(0.8f, "b.md", breadcrumb: "ORDERS > Y"),
            Hit(0.7f, "c.md", docTitle: "orders overview"),
        });

        var outcome = await sut.ReadAsync(new ReadDocsRequest("c", "q", Topic: "Orders", TopFiles: 5));
        var success = Assert.IsType<ReadDocsOutcome.Success>(outcome);
        Assert.Equal(2, success.Response.Files.Count);
        Assert.DoesNotContain(success.Response.Files, f => f.RelPath == "a.md");
    }
}
