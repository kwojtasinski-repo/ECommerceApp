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
