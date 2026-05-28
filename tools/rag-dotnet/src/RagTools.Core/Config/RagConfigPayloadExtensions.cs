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
    ///   • GlossaryTerms — per ADR-0028 Amendment 004:
    ///       null      → keep defaults (R3 fallback preserved)
    ///       []        → explicit opt-out (no expansion)
    ///       [...]     → use override verbatim (no merge)
    ///     Note: <see cref="RagConfigPayload.GlossaryTerms"/> is non-nullable List, so the null case
    ///     is represented in stored JSON by an absent property — deserialized as default ([]) which we
    ///     cannot distinguish from explicit opt-out. To preserve the three-state semantic the override
    ///     side carries an explicit sentinel: any empty list in a stored override is treated as opt-out;
    ///     to "keep defaults" the publisher must omit the override entirely.
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
            GlossaryTerms     = overridePayload.GlossaryTerms,
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
