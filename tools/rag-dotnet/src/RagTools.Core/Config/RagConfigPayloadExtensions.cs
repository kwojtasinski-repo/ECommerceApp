namespace RagTools.Core.Config;

/// <summary>
/// Merge helpers for <see cref="RagConfigPayload"/> used by <see cref="LayeredConfigSource"/>.
/// </summary>
public static class RagConfigPayloadExtensions
{
    /// <summary>
    /// Merge per-collection <paramref name="overridePayload"/> on top of mounted <paramref name="defaults"/>.
    /// Per-field rules:
    ///   • Scalars (MaxTokens, OverlapTokens, ScoreThreshold, FetchK) — override wins if non-default
    ///     (zero / unset is treated as "not specified" to allow partial overrides).
    ///   • Weights (List&lt;WeightEntry&gt;) — override wins if non-empty; otherwise keep defaults.
    ///   • GlossaryEntries — per ADR-0028 Phase 3 / P3-3 (Design B): override wins verbatim.
    ///       []        → no per-collection entries (preprocessor decides fallback: mounted or none)
    ///       [...]     → use override entries verbatim (no merge with mounted)
    ///     Note: <see cref="RagConfigPayload.GlossaryEntries"/> is a non-nullable List, so the
    ///     stored JSON cannot distinguish "absent" from "explicit empty". The two preprocessor
    ///     classes encode the fallback policy at the DI level (RAG_GLOSSARY_FALLBACK env switch).
    ///   • SchemaVersion — override wins if &gt; 0.
    ///   • HistoryField, AdrDocKind, AmendmentDocKind — override wins if non-null / non-empty.
    /// </summary>
    public static RagConfigPayload Merge(this RagConfigPayload defaults, RagConfigPayload overridePayload) =>
        new()
        {
            MaxTokens         = overridePayload.MaxTokens     > 0 ? overridePayload.MaxTokens     : defaults.MaxTokens,
            OverlapTokens     = overridePayload.OverlapTokens > 0 ? overridePayload.OverlapTokens : defaults.OverlapTokens,
            ScoreThreshold    = overridePayload.ScoreThreshold > 0f ? overridePayload.ScoreThreshold : defaults.ScoreThreshold,
            FetchK            = overridePayload.FetchK > 0 ? overridePayload.FetchK : defaults.FetchK,
            Weights           = overridePayload.Weights.Count > 0 ? overridePayload.Weights : defaults.Weights,
            GlossaryEntries   = overridePayload.GlossaryEntries,
            SchemaVersion     = overridePayload.SchemaVersion > 0 ? overridePayload.SchemaVersion : defaults.SchemaVersion,
            HistoryField      = !string.IsNullOrEmpty(overridePayload.HistoryField)
                                    ? overridePayload.HistoryField
                                    : defaults.HistoryField,
            AdrDocKind        = !string.IsNullOrEmpty(overridePayload.AdrDocKind)
                                    ? overridePayload.AdrDocKind
                                    : defaults.AdrDocKind,
            AmendmentDocKind  = !string.IsNullOrEmpty(overridePayload.AmendmentDocKind)
                                    ? overridePayload.AmendmentDocKind
                                    : defaults.AmendmentDocKind,
        };
}
