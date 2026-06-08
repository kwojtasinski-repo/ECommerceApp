using System.Text.Json;
using RagTools.Core.Query;
using RagTools.Mcp.Tools;

namespace RagTools.Tests.Tools;

/// <summary>
/// Pinning tests for <see cref="QueryDocsCachedFormatter"/>.
/// Mirrors <c>tools/rag/tests/test_query_docs_cached.py</c> — scope is now a
/// <c>Dictionary&lt;string,string&gt;</c> (scope_attrs) instead of individual bc/scope/topic params.
/// </summary>
public class QueryDocsCachedFormatterTests
{
    private static QueryHit Hit(
        string relPath,
        double score = 0.9,
        string breadcrumb = "section > sub",
        int startLine = 10,
        int endLine = 50,
        string text = "body text",
        string docKind = "doc",
        int rank = 1) =>
        new(rank, score, docKind, relPath, breadcrumb, startLine, endLine, text);

    // ── DeriveSourceLabel ─────────────────────────────────────────────────────

    [Fact]
    public void SourceLabel_ExtractsAdrId_FromAdrDashFormat()
    {
        var label = QueryDocsCachedFormatter.DeriveSourceLabel("Tell me about ADR-0029 caching", scopeAttrs: null);
        Assert.StartsWith("rag-cache-adr0029-", label);
    }

    [Fact]
    public void SourceLabel_ExtractsAdrId_FromShortForm()
    {
        var label = QueryDocsCachedFormatter.DeriveSourceLabel("explain adr 016", scopeAttrs: null);
        Assert.StartsWith("rag-cache-adr0016-", label);
    }

    [Fact]
    public void SourceLabel_ExtractsAdrId_FromBareDigits()
    {
        var label = QueryDocsCachedFormatter.DeriveSourceLabel("what does 0028 say about chunking", scopeAttrs: null);
        Assert.StartsWith("rag-cache-adr0028-", label);
    }

    [Fact]
    public void SourceLabel_UsesScopeAttrsFirstEntry()
    {
        var label = QueryDocsCachedFormatter.DeriveSourceLabel(
            "order placement flow",
            scopeAttrs: new Dictionary<string, string> { ["bc"] = "Sales / Orders" });
        Assert.StartsWith("rag-cache-bc-sales-orders-", label);
    }

    [Fact]
    public void SourceLabel_UsesArbitraryKey()
    {
        var label = QueryDocsCachedFormatter.DeriveSourceLabel(
            "catalog docs",
            scopeAttrs: new Dictionary<string, string> { ["topic"] = "Catalog" });
        Assert.StartsWith("rag-cache-topic-catalog-", label);
    }

    [Fact]
    public void SourceLabel_UsesGenericScopeKey()
    {
        var label = QueryDocsCachedFormatter.DeriveSourceLabel(
            "catalog docs",
            scopeAttrs: new Dictionary<string, string> { ["scope"] = "Catalog" });
        Assert.StartsWith("rag-cache-scope-catalog-", label);
    }

    [Fact]
    public void SourceLabel_UsesOnlyFirstEntry()
    {
        var label = QueryDocsCachedFormatter.DeriveSourceLabel(
            "some question",
            scopeAttrs: new Dictionary<string, string> { ["region"] = "PL", ["bc"] = "Orders" });
        Assert.StartsWith("rag-cache-region-pl-", label);
    }

    [Fact]
    public void SourceLabel_FallsBackToQ_WhenNoAdrNorScope()
    {
        var label = QueryDocsCachedFormatter.DeriveSourceLabel("general architecture question", scopeAttrs: null);
        Assert.StartsWith("rag-cache-q-", label);
    }

    [Fact]
    public void SourceLabel_FallsBackToQ_WhenEmptyDict()
    {
        var label = QueryDocsCachedFormatter.DeriveSourceLabel("some q", scopeAttrs: new Dictionary<string, string>());
        Assert.StartsWith("rag-cache-q-", label);
    }

    [Fact]
    public void SourceLabel_IsDeterministic_ForSameInputs()
    {
        var attrs = new Dictionary<string, string> { ["bc"] = "Catalog" };
        var a = QueryDocsCachedFormatter.DeriveSourceLabel("same question", scopeAttrs: attrs);
        var b = QueryDocsCachedFormatter.DeriveSourceLabel("same question", scopeAttrs: attrs);
        Assert.Equal(a, b);
    }

    [Fact]
    public void SourceLabel_DiffersByQuestion()
    {
        var a = QueryDocsCachedFormatter.DeriveSourceLabel("question A", scopeAttrs: null);
        var b = QueryDocsCachedFormatter.DeriveSourceLabel("question B", scopeAttrs: null);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void SourceLabel_IsLowercaseAsciiKebab()
    {
        var label = QueryDocsCachedFormatter.DeriveSourceLabel(
            "Some Q with Punctuation!",
            scopeAttrs: new Dictionary<string, string> { ["myKey"] = "BC With Spaces" });
        Assert.Matches("^[a-z0-9-]+$", label);
    }

    [Fact]
    public void SourceLabel_AdrWinsOverScopeAttrs()
    {
        var label = QueryDocsCachedFormatter.DeriveSourceLabel(
            "ADR-0028 overview",
            scopeAttrs: new Dictionary<string, string> { ["bc"] = "Catalog" });
        Assert.StartsWith("rag-cache-adr0028-", label);
    }

    // ── Build / markdown ──────────────────────────────────────────────────────

    [Fact]
    public void Markdown_ContainsHeaderAndMetadata()
    {
        var hits = new[] { Hit("docs/adr/0029/0029-foo.md") };
        var payload = QueryDocsCachedFormatter.Build(
            "How does ADR-0029 caching work?", scopeAttrs: null, topFiles: 1, hits,
            utcNow: new DateTime(2026, 5, 27, 0, 0, 0, DateTimeKind.Utc));

        Assert.Contains("# How does ADR-0029 caching work", payload.Markdown);
        Assert.Contains("> Cached from RAG on 2026-05-27", payload.Markdown);
        Assert.Contains("query_docs_cached(\"How does ADR-0029 caching work?\")", payload.Markdown);
        Assert.Contains("## 0029-foo.md", payload.Markdown);
        Assert.Contains("**Path**: `docs/adr/0029/0029-foo.md#L10-L50`", payload.Markdown);
        Assert.Contains("**Breadcrumb**: section > sub", payload.Markdown);
    }

    [Fact]
    public void Markdown_IncludesScopeAttrsJsonWhenSet()
    {
        var hits = new[] { Hit("docs/sales/orders.md") };
        var payload = QueryDocsCachedFormatter.Build(
            "order flow",
            scopeAttrs: new Dictionary<string, string> { ["bc"] = "Sales" },
            topFiles: 1, hits, utcNow: DateTime.UtcNow);
        Assert.Contains("scope_attrs={\"bc\":\"Sales\"}", payload.Markdown);
    }

    [Fact]
    public void Markdown_MultiKeyScopeAttrs_AllKeysAppear()
    {
        var hits = new[] { Hit("docs/a.md") };
        var payload = QueryDocsCachedFormatter.Build(
            "q",
            scopeAttrs: new Dictionary<string, string> { ["region"] = "PL", ["bc"] = "Orders" },
            topFiles: 1, hits, utcNow: DateTime.UtcNow);
        Assert.Contains("scope_attrs=", payload.Markdown);
        Assert.Contains("region", payload.Markdown);
        Assert.Contains("PL", payload.Markdown);
    }

    [Fact]
    public void Markdown_NoScopeArgWhenNull()
    {
        var hits = new[] { Hit("docs/a.md") };
        var payload = QueryDocsCachedFormatter.Build("q", scopeAttrs: null, topFiles: 1, hits, utcNow: DateTime.UtcNow);
        Assert.DoesNotContain("scope_attrs=", payload.Markdown);
    }

    [Fact]
    public void Build_GroupsByFile_AndRanksByBestScore()
    {
        var hits = new[]
        {
            Hit("docs/a.md", score: 0.6),
            Hit("docs/b.md", score: 0.9),
            Hit("docs/a.md", score: 0.95, startLine: 100, endLine: 120),
            Hit("docs/c.md", score: 0.5),
        };

        var payload = QueryDocsCachedFormatter.Build("q", scopeAttrs: null, topFiles: 2, hits, utcNow: DateTime.UtcNow);

        Assert.Equal(2, payload.FilesCount);
        var aIdx = payload.Markdown.IndexOf("## a.md", StringComparison.Ordinal);
        var bIdx = payload.Markdown.IndexOf("## b.md", StringComparison.Ordinal);
        Assert.True(aIdx >= 0 && bIdx > aIdx, "a.md should come before b.md");
        Assert.Contains("docs/a.md#L100-L120", payload.Markdown);
    }

    [Fact]
    public void Build_LimitsChunksPerFileToFive()
    {
        var hits = Enumerable.Range(1, 7)
            .Select(i => Hit("docs/a.md", score: 0.9 - i * 0.01, startLine: i, endLine: i + 1, text: $"chunk-{i}"))
            .ToArray();
        var payload = QueryDocsCachedFormatter.Build("q", scopeAttrs: null, topFiles: 1, hits, utcNow: DateTime.UtcNow);
        Assert.Equal(5, payload.ChunksCount);
    }

    [Fact]
    public void Build_PayloadProjection_IsSnakeCase()
    {
        var hits = new[] { Hit("docs/a.md") };
        var payload = QueryDocsCachedFormatter.Build(
            "q",
            scopeAttrs: new Dictionary<string, string> { ["bc"] = "Catalog" },
            topFiles: 1, hits, utcNow: DateTime.UtcNow);
        var json = JsonSerializer.Serialize(payload.ToProjection());
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("source", out _));
        Assert.True(root.TryGetProperty("markdown", out _));
        Assert.True(root.TryGetProperty("files_count", out _));
        Assert.True(root.TryGetProperty("chunks_count", out _));
        Assert.True(root.TryGetProperty("query", out _));
        Assert.True(root.TryGetProperty("scope_attrs", out _));
        Assert.True(root.TryGetProperty("next_step", out _));
    }

    [Fact]
    public void Slugify_CollapsesNonAlnum_AndTrims()
    {
        Assert.Equal("sales-orders", QueryDocsCachedFormatter.Slugify("Sales / Orders!!"));
        Assert.Equal("q", QueryDocsCachedFormatter.Slugify("!!!"));
        Assert.Equal("q", QueryDocsCachedFormatter.Slugify(""));
    }
}