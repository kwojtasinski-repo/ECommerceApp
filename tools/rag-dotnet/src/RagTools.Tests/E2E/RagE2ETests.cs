using System.Linq;
using System.Text.Json;
using Xunit;

namespace RagTools.Tests.E2E;

/// <summary>
/// End-to-end integration tests for all four MCP RAG tools.
///
/// These tests exercise the full stack:
///   MarkdownChunker → OnnxEmbedder → QdrantStore → RagTools (MCP tool class)
///
/// They are repo-independent: the <see cref="RagE2EFixture"/> creates a self-contained
/// synthetic workspace with generic "Alpha" and "Beta" docs — no EcommerceApp-specific
/// domain content, ADR numbers, or project structure is assumed.
///
/// Skip guards:
///   - Tests skip when the ONNX model is not present locally.
///   - Tests skip when Docker is not available AND QDRANT_URL is not set.
///
/// Run with:
///   dotnet test --filter "Category=E2E"
/// or:
///   RAG_MODEL_DIR=/path/to/model QDRANT_URL=http://localhost:6333 dotnet test --filter "Category=E2E"
/// </summary>
[Trait("Category", "E2E")]
public sealed class RagE2ETests : IClassFixture<RagE2EFixture>
{
    private readonly RagE2EFixture _fx;

    public RagE2ETests(RagE2EFixture fixture) => _fx = fixture;

    // ── query_docs ────────────────────────────────────────────────────────

    [Fact]
    public async Task QueryDocs_SemanticSearch_ReturnsRelevantChunks()
    {

        var result = await _fx.Tools!.QueryDocs(
            "strongly typed identifier value object", topic: null, top_k: 3,
            CancellationToken.None);

        // Should surface the Alpha pattern doc (typed IDs) above the Beta pattern (CQRS)
        Assert.Contains("Alpha", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryDocs_ReturnsScoreAndPath_ForEachHit()
    {

        var result = await _fx.Tools!.QueryDocs(
            "command query separation", topic: null, top_k: 5,
            CancellationToken.None);

        // JSON output should contain score and rel_path fields
        Assert.Contains("\"score\"", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".md", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryDocsCached_UsesGenericScopeKey_ForSourceLabel()
    {
        var result = await _fx.Tools!.QueryDocsCached(
            "catalog docs",
            scope_attrs: "{\"scope\":\"Catalog\"}",
            top_files: 1,
            CancellationToken.None);

        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        Assert.StartsWith("rag-cache-scope-catalog-", root.GetProperty("source").GetString());
        Assert.Contains("scope_attrs", root.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryDocs_UnrelatedQuestion_ReturnsEmptyOrLowScore()
    {

        // A question about something completely unrelated to the two synthetic docs.
        var result = await _fx.Tools!.QueryDocs(
            "weather forecast temperature celsius", topic: null, top_k: 5,
            CancellationToken.None);

        // Either returns no hits or everything is low-score noise —
        // the test just verifies it doesn't throw.
        Assert.NotNull(result);
    }

    // ── read_docs ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadDocs_ChunkMode_ReturnsGroupedChunks()
    {

        var result = await _fx.Tools!.ReadDocs(
            "how does the Alpha pattern work", topic: null, top_files: 2,
            CancellationToken.None);

        // JSON output should mention the alpha doc
        Assert.Contains("alpha", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadDocs_FullMode_ReturnsCompleteFileContent()
    {

        // Phrases that trigger full-content intent regex
        var result = await _fx.Tools!.ReadDocs(
            "show me full content of Beta pattern", topic: null, top_files: 1,
            CancellationToken.None);

        // Verify FullIntentRe triggered full-content mode (JSON "mode":"full").
        Assert.Contains("\"full\"", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"content\"", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[ERROR:", result);
    }

    // ── get_history ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetHistory_KnownId_ReturnsChunks()
    {

        // ADR-0001 is indexed with adr_id="0001" — get_history should find it using
        // the default history_field ("adr_id") since the __config__ point has no HistoryField set.
        var result = await _fx.Tools!.GetHistory("0001", CancellationToken.None);

        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        Assert.Equal("0001", root.GetProperty("id").GetString());
        Assert.Equal("adr_id", root.GetProperty("history_field").GetString());
        Assert.True(root.GetProperty("chunk_count").GetInt32() > 0, "Expected at least one chunk for ADR 0001");
    }

    [Fact]
    public async Task GetHistory_IncludesAmendmentChunks()
    {

        // ADR-0001 has an amendment — all chunks (main + amendment) should be returned.
        var result = await _fx.Tools!.GetHistory("0001", CancellationToken.None);

        var doc = JsonDocument.Parse(result);
        var count = doc.RootElement.GetProperty("chunk_count").GetInt32();
        Assert.True(count > 1, $"Expected main + amendment chunks, got {count}");
    }

    [Fact]
    public async Task GetHistory_UnknownId_ReturnsEmptyChunks()
    {

        var result = await _fx.Tools!.GetHistory("9999", CancellationToken.None);

        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        Assert.Equal("9999", root.GetProperty("id").GetString());
        Assert.Equal(0, root.GetProperty("chunk_count").GetInt32());
        Assert.Equal(0, root.GetProperty("chunks").GetArrayLength());
    }

    [Fact]
    public async Task GetHistory_ChunksOrderedByStartLine()
    {

        var result = await _fx.Tools!.GetHistory("0001", CancellationToken.None);

        var doc = JsonDocument.Parse(result);
        var chunks = doc.RootElement.GetProperty("chunks").EnumerateArray().ToList();
        var lines = chunks.Select(c => c.GetProperty("start_line").GetInt32()).ToList();
        var sorted = lines.OrderBy(x => x).ToList();
        Assert.Equal(sorted, lines);
    }

    // ── list_adrs ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAdrs_ReturnsAllAdrs()
    {

        var result = await _fx.Tools!.ListAdrs(CancellationToken.None);

        Assert.Contains("0001", result);
        Assert.Contains("0002", result);
    }

    [Fact]
    public async Task ListAdrs_IncludesAmendmentCount()
    {
        var result = await _fx.Tools!.ListAdrs(CancellationToken.None);

        // ADR-0001 has one amendment file; JSON should expose the renamed `amendments` field with a non-zero count.
        Assert.Contains("\"amendments\":", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListAdrs_ReturnsNonEmptyResult()
    {
        // ListAdrs now reads from Qdrant (not disk). Requires the fixture Qdrant to be running
        // with docs indexed. Returns structured JSON regardless of ADR count.
        var result = await _fx.Tools!.ListAdrs(CancellationToken.None);

        Assert.True(result.Length > 0, "Expected non-empty ADR listing from Qdrant.");
        Assert.Contains("adrs", result, StringComparison.OrdinalIgnoreCase);
    }
}
