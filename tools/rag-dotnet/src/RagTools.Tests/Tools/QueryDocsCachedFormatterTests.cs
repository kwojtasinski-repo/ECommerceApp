using System.Text.Json;
using RagTools.Core.Query;
using RagTools.Mcp.Tools;

namespace RagTools.Tests.Tools;

/// <summary>
/// Pinning tests for the .NET port of the Python <c>query_docs_cached</c> wrapper.
/// Mirrors <c>tools/rag/tests/test_query_docs_cached.py</c> so the two MCP servers
/// produce byte-equivalent source labels and markdown for the same hits.
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

    [Fact]
    public void SourceLabel_ExtractsAdrId_FromAdrDashFormat()
    {
        var label = QueryDocsCachedFormatter.DeriveSourceLabel("Tell me about ADR-0029 caching", bc: null);
        Assert.StartsWith("rag-cache-adr0029-", label);
    }

    [Fact]
    public void SourceLabel_ExtractsAdrId_FromShortForm()
    {
        // 3-digit form satisfies \d{3,4}
        var label = QueryDocsCachedFormatter.DeriveSourceLabel("explain adr 016", bc: null);
        Assert.StartsWith("rag-cache-adr0016-", label);
    }

    [Fact]
    public void SourceLabel_ExtractsAdrId_FromBareDigits()
    {
        var label = QueryDocsCachedFormatter.DeriveSourceLabel("what does 0028 say about chunking", bc: null);
        Assert.StartsWith("rag-cache-adr0028-", label);
    }

    [Fact]
    public void SourceLabel_FallsBackToSlugifiedBc()
    {
        var label = QueryDocsCachedFormatter.DeriveSourceLabel("order placement flow", bc: "Sales / Orders");
        Assert.StartsWith("rag-cache-sales-orders-", label);
    }

    [Fact]
    public void SourceLabel_FallsBackToQ_WhenNoAdrNorBc()
    {
        var label = QueryDocsCachedFormatter.DeriveSourceLabel("general architecture question", bc: null);
        Assert.StartsWith("rag-cache-q-", label);
    }

    [Fact]
    public void SourceLabel_IsDeterministic_ForSameInputs()
    {
        var a = QueryDocsCachedFormatter.DeriveSourceLabel("same question", bc: "Catalog");
        var b = QueryDocsCachedFormatter.DeriveSourceLabel("same question", bc: "Catalog");
        Assert.Equal(a, b);
    }

    [Fact]
    public void SourceLabel_DiffersByQuestion()
    {
        var a = QueryDocsCachedFormatter.DeriveSourceLabel("question A", bc: null);
        var b = QueryDocsCachedFormatter.DeriveSourceLabel("question B", bc: null);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void SourceLabel_IsLowercaseAsciiKebab()
    {
        var label = QueryDocsCachedFormatter.DeriveSourceLabel("Some Q with Punctuation!", bc: "BC With Spaces");
        Assert.Matches("^[a-z0-9-]+$", label);
    }

    [Fact]
    public void Markdown_ContainsHeaderAndMetadata()
    {
        var hits = new[] { Hit("docs/adr/0029/0029-foo.md") };
        var payload = QueryDocsCachedFormatter.Build(
            "How does ADR-0029 caching work?", bc: null, topFiles: 1, hits, utcNow: new DateTime(2026, 5, 27, 0, 0, 0, DateTimeKind.Utc));

        Assert.Contains("# How does ADR-0029 caching work", payload.Markdown);
        Assert.Contains("> Cached from RAG on 2026-05-27", payload.Markdown);
        Assert.Contains("query_docs_cached(\"How does ADR-0029 caching work?\")", payload.Markdown);
        Assert.Contains("## 0029-foo.md", payload.Markdown);
        Assert.Contains("**Path**: `docs/adr/0029/0029-foo.md#L10-L50`", payload.Markdown);
        Assert.Contains("**Breadcrumb**: section > sub", payload.Markdown);
    }

    [Fact]
    public void Markdown_IncludesBcArgumentWhenSet()
    {
        var hits = new[] { Hit("docs/sales/orders.md") };
        var payload = QueryDocsCachedFormatter.Build(
            "order flow", bc: "Sales", topFiles: 1, hits, utcNow: DateTime.UtcNow);
        Assert.Contains("query_docs_cached(\"order flow\", bc=\"Sales\")", payload.Markdown);
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

        var payload = QueryDocsCachedFormatter.Build("q", bc: null, topFiles: 2, hits, utcNow: DateTime.UtcNow);

        Assert.Equal(2, payload.FilesCount);
        // a.md has best score 0.95, ranked first; first chunk in its block uses startLine 100.
        var aIdx = payload.Markdown.IndexOf("## a.md", StringComparison.Ordinal);
        var bIdx = payload.Markdown.IndexOf("## b.md", StringComparison.Ordinal);
        Assert.True(aIdx >= 0 && bIdx > aIdx, "a.md should come before b.md");
        Assert.Contains("docs/a.md#L100-L120", payload.Markdown);
    }

    [Fact]
    public void Build_LimitsChunksPerFileToFive()
    {
        var hits = Enumerable.Range(1, 7).Select(i => Hit("docs/a.md", score: 0.9 - i * 0.01, startLine: i, endLine: i + 1, text: $"chunk-{i}")).ToArray();
        var payload = QueryDocsCachedFormatter.Build("q", bc: null, topFiles: 1, hits, utcNow: DateTime.UtcNow);
        Assert.Equal(5, payload.ChunksCount);
    }

    [Fact]
    public void Build_PayloadProjection_IsSnakeCase()
    {
        var hits = new[] { Hit("docs/a.md") };
        var payload = QueryDocsCachedFormatter.Build("q", bc: "Catalog", topFiles: 1, hits, utcNow: DateTime.UtcNow);
        var json = JsonSerializer.Serialize(payload.ToProjection());
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("source", out _));
        Assert.True(root.TryGetProperty("markdown", out _));
        Assert.True(root.TryGetProperty("files_count", out _));
        Assert.True(root.TryGetProperty("chunks_count", out _));
        Assert.True(root.TryGetProperty("query", out _));
        Assert.True(root.TryGetProperty("bc", out _));
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
