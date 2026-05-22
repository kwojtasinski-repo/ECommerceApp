namespace RagTools.Core;

/// <summary>
/// Serializable snapshot of the config fields that the query engine needs at runtime.
/// Stored in Qdrant per-collection (doc_kind = "__config__") so the server can serve
/// remote SSE sessions without a local config.yaml mount.
///
/// YAML is parsed once at upload time → stored as structured JSON.
/// No YAML parser needed at runtime.
/// </summary>
public sealed class RagConfigPayload
{
    // Chunker
    public int MaxTokens { get; set; } = 512;
    public int OverlapTokens { get; set; } = 64;

    // Query
    public float ScoreThreshold { get; set; } = 0.30f;
    public int FetchK { get; set; } = 20;

    // Ranking weights (pattern → multiplier, first match wins)
    public List<WeightEntry> Weights { get; set; } = [];

    // Glossary terms (expanded at query time to improve recall)
    public List<string> GlossaryTerms { get; set; } = [];

    // Schema version for future migrations
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Create a <see cref="RagConfigPayload"/> from a loaded <see cref="RagConfig"/>.</summary>
    public static RagConfigPayload From(RagConfig cfg, IEnumerable<string>? glossaryTerms = null) =>
        new()
        {
            MaxTokens      = cfg.Chunker?.MaxTokens ?? 512,
            OverlapTokens  = cfg.Chunker?.OverlapTokens ?? 64,
            ScoreThreshold = cfg.Query?.ScoreThreshold ?? 0.30f,
            FetchK         = cfg.Query?.FetchK ?? 20,
            Weights        = cfg.Ranking?.Weights ?? [],
            GlossaryTerms  = glossaryTerms?.ToList() ?? [],
            SchemaVersion  = 1,
        };
}
