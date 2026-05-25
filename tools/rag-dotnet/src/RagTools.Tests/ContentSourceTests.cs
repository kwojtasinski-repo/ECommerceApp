using RagTools.Core;
using RagTools.Core.ContentSources;

namespace RagTools.Tests;

/// <summary>
/// Unit tests for <see cref="DiskContentSource"/> and <see cref="QdrantContentSource"/>.
/// DiskContentSource tests use a real temp directory.
/// QdrantContentSource tests use a lightweight fake IDocumentStore.
/// </summary>
public sealed class ContentSourceTests : IDisposable
{
    private readonly string _tempDir;

    public ContentSourceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"content-source-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    // ── DiskContentSource ─────────────────────────────────────────────────

    private RagConfig MakeCfg() =>
        RagConfig.Load(WriteCfgFile());

    private string WriteCfgFile()
    {
        // Config must be at <workspace>/tools/rag/rag-config.yaml so that
        // RagConfig.DeriveWorkspace (which goes 3 levels up) resolves to _tempDir.
        var ragDir = Path.Combine(_tempDir, "tools", "rag");
        Directory.CreateDirectory(ragDir);
        var path = Path.Combine(ragDir, "rag-config.yaml");
        File.WriteAllText(path, $"""
            source:
              roots:
                - docs
              exclude_globs: []
            vector_store:
              backend: qdrant
              collection: test
              url: "http://localhost:6333"
            """);
        return path;
    }

    [Fact]
    public async Task DiskContentSource_ExistingFile_ReturnsContent()
    {
        var subDir = Path.Combine(_tempDir, "docs");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(subDir, "alpha.md"), "# Hello\nworld");

        var cfg = MakeCfg();
        var source = new DiskContentSource(cfg);

        var content = await source.ReadAsync("any_collection", "docs/alpha.md");

        Assert.NotNull(content);
        Assert.Contains("Hello", content);
    }

    [Fact]
    public async Task DiskContentSource_MissingFile_ReturnsNull()
    {
        var cfg = MakeCfg();
        var source = new DiskContentSource(cfg);

        var content = await source.ReadAsync("any_collection", "docs/does-not-exist.md");

        Assert.Null(content);
    }

    [Fact]
    public async Task DiskContentSource_CollectionParamIgnored_ReadsFromWorkspace()
    {
        var docsDir = Path.Combine(_tempDir, "docs");
        Directory.CreateDirectory(docsDir);
        await File.WriteAllTextAsync(Path.Combine(docsDir, "note.md"), "content here");

        var cfg = MakeCfg();
        var source = new DiskContentSource(cfg);

        // collection is irrelevant for disk reads
        var content = await source.ReadAsync("ignored_collection", "docs/note.md");
        Assert.NotNull(content);
    }

    // ── QdrantContentSource ────────────────────────────────────────────────

    [Fact]
    public async Task QdrantContentSource_ContentAvailable_ReturnsContent()
    {
        const string expectedContent = "# ADR-0001\nSome decision.";
        var store = new FakeDocumentStore("docs/adr/0001.md", expectedContent);
        var source = new QdrantContentSource(store);

        var result = await source.ReadAsync("col", "docs/adr/0001.md");

        Assert.Equal(expectedContent, result);
    }

    [Fact]
    public async Task QdrantContentSource_ContentMissing_ReturnsNull()
    {
        var store = new FakeDocumentStore("docs/adr/0001.md", "some content");
        var source = new QdrantContentSource(store);

        var result = await source.ReadAsync("col", "docs/adr/0002.md");  // different path

        Assert.Null(result);
    }

    // ── Fakes ──────────────────────────────────────────────────────────────

    private sealed class FakeDocumentStore(string knownPath, string knownContent) : IDocumentStore
    {
        public Task<ContentDocument?> FetchContentAsync(string collection, string relPath, CancellationToken ct = default)
        {
            if (relPath == knownPath)
                return Task.FromResult<ContentDocument?>(
                    new ContentDocument(relPath, "adr_main", null, relPath, knownContent, DateTimeOffset.UtcNow, null));
            return Task.FromResult<ContentDocument?>(null);
        }

        // Unused stubs
        public Task UpsertChunksAsync(string c, IReadOnlyList<RagPoint> r, CancellationToken ct = default) => Task.CompletedTask;
        public Task StoreDocumentAsync(string c, ContentDocument d, CancellationToken ct = default) => Task.CompletedTask;
        public Task StoreConfigAsync(string c, RagConfigPayload p, CancellationToken ct = default) => Task.CompletedTask;
        public Task<RagConfigPayload?> FetchConfigAsync(string c, CancellationToken ct = default) => Task.FromResult<RagConfigPayload?>(null);
        public Task DeleteByPathsAsync(string c, IEnumerable<string> r, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(string c, float[] v, SearchOptions o, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentSearchResult>>([]);
        public Task<IReadOnlyList<AdrSummary>> ListAdrsAsync(string c, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<AdrSummary>>([]);
        public Task EnsureCollectionAsync(string c, int d, CancellationToken ct = default) => Task.CompletedTask;
        public Task RecreateCollectionAsync(string c, int d, CancellationToken ct = default) => Task.CompletedTask;
        public void Dispose() { }
    }
}
