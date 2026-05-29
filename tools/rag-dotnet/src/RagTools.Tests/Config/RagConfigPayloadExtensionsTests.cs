using RagTools.Core;
using RagTools.Core.Config;

namespace RagTools.Tests.Config;

/// <summary>
/// Unit tests for <see cref="RagConfigPayloadExtensions.Merge"/> — the core function
/// that drives <see cref="LayeredConfigSource"/>. Mounted defaults + per-collection
/// Qdrant override → effective payload, override wins per-field when meaningful.
/// </summary>
public sealed class RagConfigPayloadExtensionsTests
{
    private static RagConfigPayload Defaults() => new()
    {
        MaxTokens = 512,
        OverlapTokens = 64,
        ScoreThreshold = 0.30f,
        FetchK = 20,
        Weights = [new WeightEntry { Pattern = "docs/**", Weight = 1.0f }],
        GlossaryEntries = [new GlossaryEntry("default-term", ["pl-default"])],
        SchemaVersion = 1,
        HistoryField = "adr_id",
        AdrDocKind = "adr_main",
        AmendmentDocKind = "adr_amendment",
    };

    [Fact]
    public void Merge_OverrideWins_ForNonDefaultScalars()
    {
        var defaults = Defaults();
        var ovr = new RagConfigPayload
        {
            MaxTokens = 1024,
            OverlapTokens = 128,
            ScoreThreshold = 0.50f,
            FetchK = 40,
            SchemaVersion = 2,
            HistoryField = "rfc_id",
            AdrDocKind = "rfc_main",
            AmendmentDocKind = "rfc_amendment",
        };

        var merged = defaults.Merge(ovr);

        Assert.Equal(1024, merged.MaxTokens);
        Assert.Equal(128, merged.OverlapTokens);
        Assert.Equal(0.50f, merged.ScoreThreshold);
        Assert.Equal(40, merged.FetchK);
        Assert.Equal(2, merged.SchemaVersion);
        Assert.Equal("rfc_id", merged.HistoryField);
        Assert.Equal("rfc_main", merged.AdrDocKind);
        Assert.Equal("rfc_amendment", merged.AmendmentDocKind);
    }

    [Fact]
    public void Merge_DefaultsWin_WhenOverrideScalarsAreZeroOrEmpty()
    {
        var defaults = Defaults();
        var ovr = new RagConfigPayload(); // all defaults (MaxTokens=512 etc.) — but explicitly 0 won't survive
        ovr.MaxTokens = 0;
        ovr.OverlapTokens = 0;
        ovr.ScoreThreshold = 0f;
        ovr.FetchK = 0;
        ovr.SchemaVersion = 0;
        ovr.HistoryField = "";
        ovr.AdrDocKind = null;
        ovr.AmendmentDocKind = null;

        var merged = defaults.Merge(ovr);

        Assert.Equal(512, merged.MaxTokens);
        Assert.Equal(64, merged.OverlapTokens);
        Assert.Equal(0.30f, merged.ScoreThreshold);
        Assert.Equal(20, merged.FetchK);
        Assert.Equal(1, merged.SchemaVersion);
        Assert.Equal("adr_id", merged.HistoryField);
        Assert.Equal("adr_main", merged.AdrDocKind);
        Assert.Equal("adr_amendment", merged.AmendmentDocKind);
    }

    [Fact]
    public void Merge_OverrideWeightsWin_WhenNonEmpty()
    {
        var defaults = Defaults();
        var ovr = new RagConfigPayload
        {
            Weights = [new WeightEntry { Pattern = "specs/**", Weight = 1.5f }],
        };

        var merged = defaults.Merge(ovr);

        Assert.Single(merged.Weights);
        Assert.Equal("specs/**", merged.Weights[0].Pattern);
        Assert.Equal(1.5f, merged.Weights[0].Weight);
    }

    [Fact]
    public void Merge_DefaultWeightsWin_WhenOverrideWeightsEmpty()
    {
        var defaults = Defaults();
        var ovr = new RagConfigPayload { Weights = [] };

        var merged = defaults.Merge(ovr);

        Assert.Single(merged.Weights);
        Assert.Equal("docs/**", merged.Weights[0].Pattern);
    }

    [Fact]
    public void Merge_GlossaryEntries_OverrideWinsVerbatim_NoMerging()
    {
        // Per ADR-0028 Phase 3 / P3-3 (Design B): override's GlossaryEntries is taken verbatim.
        // The two preprocessor classes (Mounted/DbOnly) encode the empty-list fallback policy
        // at the DI level; Merge itself never combines mounted and override entries.
        var defaults = Defaults();
        var ovr = new RagConfigPayload
        {
            GlossaryEntries = [new GlossaryEntry("override-only", ["pl-override"])],
        };

        var merged = defaults.Merge(ovr);

        Assert.Single(merged.GlossaryEntries);
        Assert.Equal("override-only", merged.GlossaryEntries[0].English);
        Assert.Equal("pl-override", merged.GlossaryEntries[0].Patterns[0]);
    }

    [Fact]
    public void Merge_GlossaryEntries_EmptyOverride_ReplacesDefaults()
    {
        // Empty list survives the merge verbatim (no fallback to mounted entries inside Merge).
        // The preprocessor class chosen at DI time decides whether "empty" means
        // "use mounted YAML" (MountedFallback...) or "no expansion" (DbOnly...).
        var defaults = Defaults();
        var ovr = new RagConfigPayload { GlossaryEntries = [] };

        var merged = defaults.Merge(ovr);

        Assert.Empty(merged.GlossaryEntries);
    }
}
