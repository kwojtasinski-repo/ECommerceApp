using RagTools.Core;
using RagTools.Core.Config;

namespace RagTools.Tests.Config;

/// <summary>
/// Unit tests for the three plain <see cref="IConfigSource"/> implementations.
/// Cache decorator <see cref="CachingConfigSource"/> has its own file.
/// </summary>
public sealed class IConfigSourceTests : IDisposable
{
    private readonly string _tempDir;

    public IConfigSourceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"configsrc-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private RagConfig BuildMountedConfig()
    {
        var yaml = """
            source:
              roots: [docs]
              exclude_globs: []
            chunker:
              max_tokens: 512
              min_tokens: 40
              overlap_tokens: 64
              split_on_headings: [1, 2, 3]
            vector_store:
              backend: qdrant
              mode: memory
              collection: test_collection
              url: "http://localhost:6333"
            ranking:
              weights:
                - { pattern: "docs/**", weight: 1.0 }
            query:
              default_top_k: 5
              fetch_k: 20
              score_threshold: 0.30
            """;
        var path = Path.Combine(_tempDir, "rag-config.yaml");
        File.WriteAllText(path, yaml);
        return RagConfig.Load(path);
    }
    // ── Minimal in-test IDocumentStore that only implements config fetch/store. ──
    private sealed class StubStore : IDocumentStore
    {
        public RagConfigPayload? Stored { get; set; }
        public int FetchCount { get; private set; }

        public Task<RagConfigPayload?> FetchConfigAsync(string collection, CancellationToken ct = default)
        {
            FetchCount++;
            return Task.FromResult(Stored);
        }
        public Task StoreConfigAsync(string collection, RagConfigPayload payload, CancellationToken ct = default)
        {
            Stored = payload;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(string c, float[] v, SearchOptions o, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpsertChunksAsync(string c, IReadOnlyList<RagPoint> chunks, CancellationToken ct = default) => throw new NotImplementedException();
        public Task StoreDocumentAsync(string c, ContentDocument doc, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ContentDocument?> FetchContentAsync(string c, string relPath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task EnsureCollectionAsync(int dimensions, CancellationToken ct = default) => throw new NotImplementedException();
        public Task EnsureCollectionAsync(string c, int dimensions, CancellationToken ct = default) => throw new NotImplementedException();
        public Task RecreateCollectionAsync(string c, int dimensions, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeletePointsForPathAsync(string c, string relPath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteByPathsAsync(string c, IEnumerable<string> relPaths, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteCollectionAsync(string c, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AdrSummary>> ListAdrsAsync(string c, CancellationToken ct = default) => throw new NotImplementedException();
        public void Dispose() { }
    }

    // ── FileConfigSource ──────────────────────────────────────────────────────

    [Fact]
    public async Task FileConfigSource_AlwaysReturnsMountedPayload_RegardlessOfCollection()
    {
        var sut = new FileConfigSource(BuildMountedConfig());

        var a = await sut.GetEffectiveAsync("collection-a");
        var b = await sut.GetEffectiveAsync("collection-b");

        Assert.Same(a, b); // same cached payload instance
    }

    [Fact]
    public void FileConfigSource_Invalidate_IsNoOp()
    {
        var sut = new FileConfigSource(BuildMountedConfig());
        sut.Invalidate("anything"); // must not throw
    }

    // ── QdrantConfigSource ────────────────────────────────────────────────────

    [Fact]
    public async Task QdrantConfigSource_ReturnsStoredPayload_WhenPresent()
    {
        var store = new StubStore { Stored = new RagConfigPayload { FetchK = 99 } };
        var sut = new QdrantConfigSource(store);

        var effective = await sut.GetEffectiveAsync("any");

        Assert.Equal(99, effective.FetchK);
    }

    [Fact]
    public async Task QdrantConfigSource_Throws_WhenNothingStored()
    {
        var store = new StubStore { Stored = null };
        var sut = new QdrantConfigSource(store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await sut.GetEffectiveAsync("uningested"));
    }

    // ── LayeredConfigSource ───────────────────────────────────────────────────

    [Fact]
    public async Task LayeredConfigSource_ReturnsDefaults_WhenNoOverrideStored()
    {
        var defaults = new FileConfigSource(BuildMountedConfig());
        var store = new StubStore { Stored = null };
        var sut = new LayeredConfigSource(defaults, store);

        var expected = await defaults.GetEffectiveAsync("any");
        var actual = await sut.GetEffectiveAsync("any");

        Assert.Equal(expected.MaxTokens, actual.MaxTokens);
        Assert.Equal(expected.FetchK, actual.FetchK);
    }

    [Fact]
    public async Task LayeredConfigSource_MergesOverride_OverDefaults()
    {
        var defaults = new FileConfigSource(BuildMountedConfig());
        var store = new StubStore
        {
            Stored = new RagConfigPayload
            {
                FetchK = 77,
                Weights = [new WeightEntry { Pattern = "x/**", Weight = 2.0f }],
                GlossaryTerms = ["override"],
            },
        };
        var sut = new LayeredConfigSource(defaults, store);

        var effective = await sut.GetEffectiveAsync("any");

        Assert.Equal(77, effective.FetchK);
        Assert.Single(effective.Weights);
        Assert.Equal("x/**", effective.Weights[0].Pattern);
        Assert.Single(effective.GlossaryTerms);
        Assert.Equal("override", effective.GlossaryTerms[0]);
    }
}
