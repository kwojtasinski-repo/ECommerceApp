using Microsoft.Extensions.Logging.Abstractions;
using RagTools.Core;
using RagTools.Core.Primitives;
using Xunit;

namespace RagTools.Tests;

public class DocumentProcessorTests
{
    private const string Coll = "ecommerceapp_test";

    // ── Fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeEmbedder : IEmbedder
    {
        public int Dimensions { get; } = 8;
        public int BatchCallCount { get; private set; }
        public int TotalTextsEmbedded { get; private set; }
        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
            => Task.FromResult(new float[Dimensions]);
        public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        {
            BatchCallCount++;
            TotalTextsEmbedded += texts.Count;
            return Task.FromResult(texts.Select(_ => new float[Dimensions]).ToArray());
        }
        public void Dispose() { }
    }

    private sealed class FakeStore : IDocumentStore
    {
        public List<string> EnsureCollectionCalls { get; } = [];
        public List<(string Coll, string[] Paths)> DeleteByPathsCalls { get; } = [];
        public List<(string Coll, IReadOnlyList<RagPoint> Points)> UpsertCalls { get; } = [];
        public List<(string Coll, ContentDocument Doc)> StoreDocumentCalls { get; } = [];

        public Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
        { EnsureCollectionCalls.Add(collection); return Task.CompletedTask; }

        public Task DeleteByPathsAsync(string collection, IEnumerable<string> relPaths, CancellationToken ct = default)
        { DeleteByPathsCalls.Add((collection, relPaths.ToArray())); return Task.CompletedTask; }

        public Task UpsertChunksAsync(string collection, IReadOnlyList<RagPoint> chunks, CancellationToken ct = default)
        { UpsertCalls.Add((collection, chunks)); return Task.CompletedTask; }

        public Task StoreDocumentAsync(string collection, ContentDocument doc, CancellationToken ct = default)
        { StoreDocumentCalls.Add((collection, doc)); return Task.CompletedTask; }

        // Unused for these tests
        public Task StoreConfigAsync(string collection, RagConfigPayload config, CancellationToken ct = default) => Task.CompletedTask;
        public Task<RagConfigPayload?> FetchConfigAsync(string collection, CancellationToken ct = default) => Task.FromResult<RagConfigPayload?>(null);
        public Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(string collection, float[] queryVector, SearchOptions opts, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentSearchResult>>([]);
        public Task<ContentDocument?> FetchContentAsync(string collection, string relPath, CancellationToken ct = default) => Task.FromResult<ContentDocument?>(null);
        public Task<IReadOnlyList<AdrSummary>> ListAdrsAsync(string collection, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<AdrSummary>>([]);
        public Task RecreateCollectionAsync(string collection, int dimensions, CancellationToken ct = default) => Task.CompletedTask;
        public void Dispose() { }
    }

    private static MarkdownChunker BuildChunker(int maxTokens = 800, int minTokens = 1)
        => new(new ChunkerSection { MaxTokens = maxTokens, MinTokens = minTokens, OverlapTokens = 10 },
               BertTokenCounter.FromModelDir("/nonexistent/path"));

    private static RagConfig BuildConfig(params (string Pattern, float Weight)[] weights)
    {
        var cfg = new RagConfig();
        foreach (var (p, w) in weights)
            cfg.Ranking.Weights.Add(new WeightEntry { Pattern = p, Weight = w });
        return cfg;
    }

    private static DocumentProcessor BuildProcessor(
        out FakeEmbedder embedder, out FakeStore store, RagConfig? cfg = null)
    {
        embedder = new FakeEmbedder();
        store    = new FakeStore();
        var chunker = BuildChunker();
        return new DocumentProcessor(cfg ?? new RagConfig(), chunker, embedder, store, NullLogger<DocumentProcessor>.Instance);
    }

    private static DocumentProcessingRequest Req(
        string relPath, string content, bool ensure = false, bool storeFull = false)
        => new(CollectionName.Parse(Coll), relPath, content,
               EnsureCollection: ensure, StoreFullContent: storeFull);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_DeletesByPathThenUpserts()
    {
        var sut = BuildProcessor(out _, out var store);
        await sut.ProcessAsync(Req("docs/a.md", "# Hello\n\nBody."));

        Assert.Single(store.DeleteByPathsCalls);
        Assert.Equal(Coll, store.DeleteByPathsCalls[0].Coll);
        Assert.Equal("docs/a.md", Assert.Single(store.DeleteByPathsCalls[0].Paths));
        Assert.Single(store.UpsertCalls);
        Assert.Equal(Coll, store.UpsertCalls[0].Coll);
        Assert.NotEmpty(store.UpsertCalls[0].Points);
    }

    [Fact]
    public async Task ProcessAsync_EnsureCollectionFalse_DoesNotCallEnsure()
    {
        var sut = BuildProcessor(out _, out var store);
        await sut.ProcessAsync(Req("docs/a.md", "# x"));
        Assert.Empty(store.EnsureCollectionCalls);
    }

    [Fact]
    public async Task ProcessAsync_EnsureCollectionTrue_CallsEnsureBeforeUpsert()
    {
        var sut = BuildProcessor(out _, out var store);
        await sut.ProcessAsync(Req("docs/a.md", "# x", ensure: true));
        Assert.Equal(Coll, Assert.Single(store.EnsureCollectionCalls));
    }

    [Fact]
    public async Task ProcessAsync_StoreFullContentFalse_DoesNotStoreDocument()
    {
        var sut = BuildProcessor(out _, out var store);
        await sut.ProcessAsync(Req("docs/a.md", "# x"));
        Assert.Empty(store.StoreDocumentCalls);
    }

    [Fact]
    public async Task ProcessAsync_StoreFullContentTrue_StoresFullDocument()
    {
        var sut = BuildProcessor(out _, out var store);
        await sut.ProcessAsync(Req("docs/a.md", "# Hello\n\nBody.", storeFull: true));
        var (coll, doc) = Assert.Single(store.StoreDocumentCalls);
        Assert.Equal(Coll, coll);
        Assert.Equal("docs/a.md", doc.RelPath);
        Assert.Equal("Hello", doc.Title);
        Assert.Contains("Body.", doc.Content);
    }

    [Fact]
    public async Task ProcessAsync_SanitizesReplacementChars()
    {
        var sut = BuildProcessor(out _, out var store);
        await sut.ProcessAsync(Req("docs/bad.md", "# T\n\nCaf\uFFFD au lait.", storeFull: true));
        var doc = store.StoreDocumentCalls[0].Doc;
        Assert.DoesNotContain('\uFFFD', doc.Content);
        Assert.Contains("Caf? au lait.", doc.Content);
    }

    [Fact]
    public async Task ProcessAsync_AppliesPerFileWeightToEveryChunk()
    {
        var cfg = BuildConfig(("docs/**", 1.25f));
        var sut = BuildProcessor(out _, out var store, cfg);
        var result = await sut.ProcessAsync(Req("docs/a.md", "# T\n\nbody"));
        Assert.Equal(1.25f, result.Weight);
        Assert.All(store.UpsertCalls[0].Points, p => Assert.Equal(1.25f, p.Payload.Weight));
    }

    [Fact]
    public async Task ProcessAsync_NoMatchingWeight_DefaultsToOne()
    {
        var sut = BuildProcessor(out _, out _);
        var result = await sut.ProcessAsync(Req("docs/a.md", "# T\n\nbody"));
        Assert.Equal(1.0f, result.Weight);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsTitleFromH1()
    {
        var sut = BuildProcessor(out _, out _);
        var result = await sut.ProcessAsync(Req("docs/a.md", "# Hello World\n\nbody"));
        Assert.Equal("Hello World", result.Title);
    }

    [Fact]
    public async Task ProcessAsync_NoH1_FallsBackToRelPath()
    {
        var sut = BuildProcessor(out _, out _);
        var result = await sut.ProcessAsync(Req("docs/a.md", "no heading here"));
        Assert.Equal("docs/a.md", result.Title);
    }

    [Fact]
    public async Task ProcessAsync_DocKindOverride_TakesPrecedence()
    {
        var sut = BuildProcessor(out _, out var store);
        var result = await sut.ProcessAsync(new DocumentProcessingRequest(
            CollectionName.Parse(Coll), "docs/a.md", "# T", DocKindOverride: "custom_kind"));
        Assert.Equal("custom_kind", result.DocKind);
        Assert.Equal("custom_kind", store.UpsertCalls[0].Points[0].Payload.DocKind);
    }

    [Fact]
    public async Task ProcessAsync_AdrIdOverride_TakesPrecedence()
    {
        var sut = BuildProcessor(out _, out var store);
        var result = await sut.ProcessAsync(new DocumentProcessingRequest(
            CollectionName.Parse(Coll), "docs/a.md", "# T", AdrIdOverride: "9999"));
        Assert.Equal("9999", result.AdrId);
        Assert.Equal("9999", store.UpsertCalls[0].Points[0].Payload.AdrId);
    }

    [Fact]
    public async Task ProcessAsync_EmbedsInBatchesOf32()
    {
        // Build content with ~70 H2 sections, MinTokens=1, MaxTokens small → many chunks.
        var body = string.Join("\n\n",
            Enumerable.Range(0, 70).Select(i => $"## H{i}\n\nLine {i}."));
        var content = "# Doc\n\n" + body;

        var embedder = new FakeEmbedder();
        var store    = new FakeStore();
        var chunker  = new MarkdownChunker(
            new ChunkerSection { MaxTokens = 10, MinTokens = 1, OverlapTokens = 0, SplitOnHeadings = [1, 2] },
            BertTokenCounter.FromModelDir("/nonexistent/path"));
        var sut = new DocumentProcessor(new RagConfig(), chunker, embedder, store, NullLogger<DocumentProcessor>.Instance);

        var result = await sut.ProcessAsync(Req("docs/big.md", content));

        Assert.True(result.ChunkCount > 32, $"expected >32 chunks, got {result.ChunkCount}");
        Assert.True(embedder.BatchCallCount >= 2,
            $"expected ≥2 batch calls for {result.ChunkCount} chunks, got {embedder.BatchCallCount}");
        Assert.Equal(result.ChunkCount, embedder.TotalTextsEmbedded);
    }

    [Fact]
    public async Task ProcessAsync_FileSizeBytesOverride_UsedForStubRule()
    {
        // example-implementation file: below threshold should produce 0.05 weight regardless of content length
        var sut = BuildProcessor(out _, out _);
        var result = await sut.ProcessAsync(new DocumentProcessingRequest(
            CollectionName.Parse(Coll),
            "docs/example-implementation/x.md",
            new string('x', 5000), // content is long
            FileSizeBytes: 100));  // but override says it's tiny
        Assert.Equal(0.05f, result.Weight);
    }

    [Fact]
    public async Task ProcessAsync_CancellationRequested_Throws()
    {
        var sut = BuildProcessor(out _, out _);
        var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            sut.ProcessAsync(Req("docs/a.md", "# x"), cts.Token));
    }
}
