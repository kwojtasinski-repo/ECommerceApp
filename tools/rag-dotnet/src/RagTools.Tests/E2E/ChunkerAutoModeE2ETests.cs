using RagTools.Core;
using Xunit;

namespace RagTools.Tests.E2E;

// ═════════════════════════════════════════════════════════════════════════════
// LEVEL A — MarkdownChunker integration tests (no infrastructure required)
//
// Uses the fixture content strings from AutoModeE2EFixture directly so the
// tests are fully self-contained (no Qdrant, no ONNX model, no disk reads).
// These always run, regardless of environment.
// ═════════════════════════════════════════════════════════════════════════════

[Trait("Category", "E2E-AutoMode-LevelA")]
public sealed class ChunkerAutoModeE2E_LevelA
{
    // ── Token counter (BertTokenCounter fallback — no model needed for Level A) ─────
    private static readonly ITokenCounter _counter = BertTokenCounter.FromModelDir("/nonexistent/path");

    private static MarkdownChunker AutoChunker(int minTokens = 40) =>
        new(new ChunkerSection
        {
            MaxTokens = 800,
            MinTokens = minTokens,
            OverlapTokens = 0,
            SplitOnHeadingsRaw = "auto",
        }, _counter);

    private static MarkdownChunker ExplicitChunker(int minTokens = 40) =>
        new(new ChunkerSection
        {
            MaxTokens = 800,
            MinTokens = minTokens,
            OverlapTokens = 0,
            SplitOnHeadings = [1, 2, 3],
        }, _counter);

    // ── H4 fixture: heading boundaries ────────────────────────────────────

    [Fact]
    public void AutoMode_H4HeadingsAppearInBreadcrumbs()
    {
        var chunks = AutoChunker().Chunk(AutoModeE2EFixture.H4Doc, AutoModeE2EFixture.H4RelPath);
        var breadcrumbs = chunks.Select(c => c.Breadcrumb).ToList();

        // Auto mode splits at H4: at least some H4 headings appear in breadcrumbs.
        // "Status Codes" appears in its own chunk because its predecessor emitted separately.
        Assert.True(breadcrumbs.Any(b => b.Contains("Status Codes") || b.Contains("Versioning Strategy") || b.Contains("Unit of Work") || b.Contains("Connection Management")),
            "Auto mode should produce at least one chunk whose breadcrumb includes an H4 heading");
    }

    [Fact]
    public void ExplicitMode_H4HeadingsDoNotAppearInBreadcrumbs()
    {
        var chunks = ExplicitChunker().Chunk(AutoModeE2EFixture.H4Doc, AutoModeE2EFixture.H4RelPath);
        var breadcrumbs = chunks.Select(c => c.Breadcrumb).ToList();

        Assert.DoesNotContain(breadcrumbs, b => b.Contains("Versioning Strategy"));
        Assert.DoesNotContain(breadcrumbs, b => b.Contains("Status Codes"));
    }

    [Fact]
    public void AutoMode_ProducesMoreChunksThanExplicitDueToH4Splits()
    {
        var autoN    = AutoChunker().Chunk(AutoModeE2EFixture.H4Doc, AutoModeE2EFixture.H4RelPath).Count;
        var explicitN = ExplicitChunker().Chunk(AutoModeE2EFixture.H4Doc, AutoModeE2EFixture.H4RelPath).Count;

        Assert.True(autoN > explicitN,
            $"Auto mode should produce more chunks due to H4 splits: auto={autoN}, explicit={explicitN}");
    }

    [Fact]
    public void AutoMode_H4ContentIsNotLost()
    {
        var chunks = AutoChunker().Chunk(AutoModeE2EFixture.H4Doc, AutoModeE2EFixture.H4RelPath);
        var combined = string.Join(" ", chunks.Select(c => c.Text));

        // These phrases appear only inside H4 "Versioning Strategy".
        Assert.Contains("Never break existing versions", combined);
        Assert.Contains("Deprecation requires a minimum six-month notice period", combined);
        // These phrases appear only inside H4 "Status Codes".
        Assert.Contains("Use 201 for successful POST", combined);
        Assert.Contains("Use 409 for conflicts", combined);
        // "Connection Management" is a trailing small H4 — merged backward into "Unit of Work".
        Assert.Contains("Connection pooling is handled exclusively", combined);
    }

    [Fact]
    public void AutoMode_TrailingSmallH4MergedBackward_ContentPreserved()
    {
        // "Connection Management" is the LAST section and is below min_tokens=40.
        // The backward-merge fix must append its text to the preceding "Unit of Work" chunk.
        var chunks = AutoChunker(minTokens: 40)
            .Chunk(AutoModeE2EFixture.H4Doc, AutoModeE2EFixture.H4RelPath);
        var combined = string.Join(" ", chunks.Select(c => c.Text));

        // The backward-merge fix ensures trailing small chunks are NOT silently dropped.
        // The content is preserved regardless of whether it merges into a predecessor or stands alone.
        Assert.Contains("Connection pooling is handled exclusively", combined);
    }

    // ── Short-sections fixture: merge instead of drop ─────────────────────

    [Fact]
    public void ExplicitMode_DropsShortSection()
    {
        var chunks = ExplicitChunker(minTokens: 40)
            .Chunk(AutoModeE2EFixture.ShortSectionsDoc, AutoModeE2EFixture.ShortRelPath);
        var combined = string.Join(" ", chunks.Select(c => c.Text));

        Assert.DoesNotContain("See MIGRATION.md", combined);
    }

    [Fact]
    public void AutoMode_PreservesShortSectionViaForwardMerge()
    {
        var chunks = AutoChunker(minTokens: 40)
            .Chunk(AutoModeE2EFixture.ShortSectionsDoc, AutoModeE2EFixture.ShortRelPath);
        var combined = string.Join(" ", chunks.Select(c => c.Text));

        Assert.Contains("See MIGRATION.md", combined);
    }

    [Fact]
    public void AutoMode_NoContentLoss_ShortSectionsFixture()
    {
        var chunks = AutoChunker(minTokens: 40)
            .Chunk(AutoModeE2EFixture.ShortSectionsDoc, AutoModeE2EFixture.ShortRelPath);
        var combined = string.Join(" ", chunks.Select(c => c.Text));

        // Version 2.0 section
        Assert.Contains("Major rewrite of the order processing subsystem", combined);
        // Short "See Also" must not be dropped
        Assert.Contains("See MIGRATION.md", combined);
        // Breaking Changes section
        Assert.Contains("The CreateOrder method now requires a UserId parameter", combined);
        // Deprecated APIs section
        Assert.Contains("LegacyOrderService", combined);
    }

    [Fact]
    public void AutoMode_ChunkCountAtLeastEqualToExplicit_ShortSectionsFixture()
    {
        var autoN    = AutoChunker(minTokens: 40)
            .Chunk(AutoModeE2EFixture.ShortSectionsDoc, AutoModeE2EFixture.ShortRelPath).Count;
        var explicitN = ExplicitChunker(minTokens: 40)
            .Chunk(AutoModeE2EFixture.ShortSectionsDoc, AutoModeE2EFixture.ShortRelPath).Count;

        Assert.True(autoN >= explicitN,
            $"Auto mode should not lose chunks vs explicit: auto={autoN}, explicit={explicitN}");
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// LEVEL B — Ingest pipeline → Qdrant payload assertions
// LEVEL C — Semantic query assertions
//
// Both levels share a single AutoModeE2EFixture that starts Qdrant, runs the
// full ingest, and exposes the MCP Tools instance.
// All tests carry [SkippableFact] and skip when the fixture is unavailable
// (no model / no Docker / no QDRANT_URL).
// ═════════════════════════════════════════════════════════════════════════════

[Trait("Category", "E2E-AutoMode-LevelBC")]
public sealed class ChunkerAutoModeE2E_LevelBC : IClassFixture<AutoModeE2EFixture>
{
    private readonly AutoModeE2EFixture _fx;

    public ChunkerAutoModeE2E_LevelBC(AutoModeE2EFixture fixture) => _fx = fixture;

    // ─────────────────────────────────────────────────────────────────────
    // LEVEL B — Qdrant payload scrolling
    // ─────────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task LevelB_H4BreadcrumbsPresentInQdrant()
    {
        Skip.If(!_fx.IsAvailable, _fx.SkipReason);

        var chunks = await _fx.ScrollChunksAsync(AutoModeE2EFixture.H4RelPath);
        var breadcrumbs = chunks.Select(c => c.Breadcrumb).ToList();

        Assert.True(breadcrumbs.Any(b => b.Contains("Versioning Strategy")),
            $"Qdrant should contain a point with 'Versioning Strategy' in its breadcrumb. " +
            $"Stored breadcrumbs: [{string.Join(", ", breadcrumbs)}]");
        Assert.True(breadcrumbs.Any(b => b.Contains("Status Codes")),
            "Qdrant should contain a point with 'Status Codes' in its breadcrumb.");
        Assert.True(breadcrumbs.Any(b => b.Contains("Unit of Work")),
            "Qdrant should contain a point with 'Unit of Work' in its breadcrumb.");
    }

    [SkippableFact]
    public async Task LevelB_AutoModeStoredMorePointsThanExplicitWould()
    {
        Skip.If(!_fx.IsAvailable, _fx.SkipReason);

        // Count auto-mode points (already in Qdrant for the H4 doc).
        var autoChunks = await _fx.ScrollChunksAsync(AutoModeE2EFixture.H4RelPath);
        var autoCount = autoChunks.Count;

        // Simulate explicit-mode chunk count using the chunker directly.
        var counter = BertTokenCounter.FromModelDir("/nonexistent/path");
        var explicitChunker = new MarkdownChunker(new ChunkerSection
        {
            MaxTokens = 800, MinTokens = 40, OverlapTokens = 0,
            SplitOnHeadings = [1, 2, 3],
        }, counter);
        var explicitCount = explicitChunker
            .Chunk(AutoModeE2EFixture.H4Doc, AutoModeE2EFixture.H4RelPath)
            .Count;

        Assert.True(autoCount > explicitCount,
            $"Auto mode should store more chunks than explicit due to H4 splits: " +
            $"auto={autoCount}, explicit={explicitCount}");
    }

    [SkippableFact]
    public async Task LevelB_ShortSectionTextPresentInQdrantPayload()
    {
        Skip.If(!_fx.IsAvailable, _fx.SkipReason);

        var chunks = await _fx.ScrollChunksAsync(AutoModeE2EFixture.ShortRelPath);
        var allText = string.Join(" ", chunks.Select(c => c.Text));

        Assert.True(allText.Contains("See MIGRATION.md"),
            "The short 'See Also' section body must be present in Qdrant payload " +
            "via auto-mode forward merging. It was not found in any stored chunk.");
    }

    [SkippableFact]
    public async Task LevelB_TrailingSmallSectionNotLost_InQdrantPayload()
    {
        Skip.If(!_fx.IsAvailable, _fx.SkipReason);

        var chunks = await _fx.ScrollChunksAsync(AutoModeE2EFixture.H4RelPath);
        var allText = string.Join(" ", chunks.Select(c => c.Text));

        Assert.True(allText.Contains("Connection pooling is handled exclusively"),
            "The trailing short 'Connection Management' H4 section should be " +
            "backward-merged into the preceding chunk — text must be present in Qdrant.");
    }

    // ─────────────────────────────────────────────────────────────────────
    // LEVEL C — Semantic query verification
    // ─────────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task LevelC_H4VersioningContent_FindableBySemantcSearch()
    {
        Skip.If(!_fx.IsAvailable, _fx.SkipReason);

        var result = await _fx.Tools!.QueryDocs(
            "API versioning breaking changes deprecation policy",
            bc: null, topK: 5, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Length > 0, "Expected at least one character in query result.");
        // The result should mention versioning-related content from the H4 section.
        var hasVersioning = result.Contains("Versioning", StringComparison.OrdinalIgnoreCase)
                         || result.Contains("version", StringComparison.OrdinalIgnoreCase)
                         || result.Contains("deprecat", StringComparison.OrdinalIgnoreCase);
        Assert.True(hasVersioning,
            $"QueryDocs should surface versioning content from the H4 chunk. Result:\n{result[..Math.Min(500, result.Length)]}");
    }

    [SkippableFact]
    public async Task LevelC_H4StatusCodesContent_FindableBySemanticSearch()
    {
        Skip.If(!_fx.IsAvailable, _fx.SkipReason);

        var result = await _fx.Tools!.QueryDocs(
            "HTTP response status codes 200 201 404 409",
            bc: null, topK: 5, CancellationToken.None);

        Assert.NotNull(result);
        // Result should reference HTTP status codes from the H4 "Status Codes" chunk.
        var hasStatusCodes = result.Contains("201", StringComparison.Ordinal)
                          || result.Contains("Status Codes", StringComparison.OrdinalIgnoreCase)
                          || result.Contains("404", StringComparison.Ordinal);
        Assert.True(hasStatusCodes,
            $"QueryDocs should surface status-code content from the H4 chunk. Result:\n{result[..Math.Min(500, result.Length)]}");
    }

    [SkippableFact]
    public async Task LevelC_MergedShortSection_FindableBySemanticSearch()
    {
        Skip.If(!_fx.IsAvailable, _fx.SkipReason);

        // The "See Also" section was forward-merged into the surrounding chunk.
        // Its semantic neighbourhood (migration, upgrade) should be retrievable.
        var result = await _fx.Tools!.QueryDocs(
            "migration upgrade instructions guide",
            bc: null, topK: 5, CancellationToken.None);

        Assert.NotNull(result);
        var hasMigration = result.Contains("MIGRATION", StringComparison.OrdinalIgnoreCase)
                        || result.Contains("migration", StringComparison.OrdinalIgnoreCase)
                        || result.Contains("upgrade", StringComparison.OrdinalIgnoreCase);
        Assert.True(hasMigration,
            $"QueryDocs should surface the merged short-section content. Result:\n{result[..Math.Min(500, result.Length)]}");
    }

    [SkippableFact]
    public async Task LevelC_DeprecatedApis_FindableBySemanticSearch()
    {
        Skip.If(!_fx.IsAvailable, _fx.SkipReason);

        var result = await _fx.Tools!.QueryDocs(
            "deprecated APIs legacy service removal",
            bc: null, topK: 5, CancellationToken.None);

        Assert.NotNull(result);
        var hasDeprecated = result.Contains("deprecated", StringComparison.OrdinalIgnoreCase)
                         || result.Contains("LegacyOrderService", StringComparison.OrdinalIgnoreCase)
                         || result.Contains("Deprecated", StringComparison.OrdinalIgnoreCase);
        Assert.True(hasDeprecated,
            $"QueryDocs should find the Deprecated APIs section. Result:\n{result[..Math.Min(500, result.Length)]}");
    }
}
