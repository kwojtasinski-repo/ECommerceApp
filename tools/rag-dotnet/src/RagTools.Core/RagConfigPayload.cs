namespace RagTools.Core;

/// <summary>
/// Serializable snapshot of the config fields that the query engine needs at runtime.
/// Stored in Qdrant per-collection (doc_kind = "__config__") so the server can serve
/// remote HTTP sessions without a local rag-config.yaml mount.
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

    // Per-collection multilingual glossary entries (expanded at query time to improve recall).
    // P3-3 (ADR-0028): full per-collection entries (English → patterns). Replaced the earlier
    // GlossaryTerms allow-list field. Schema bumped to 2.
    public List<GlossaryEntry> GlossaryEntries { get; set; } = [];

    // Schema version for future migrations.
    // 2 — GlossaryTerms (List<string> allow-list) replaced by GlossaryEntries (List<GlossaryEntry>).
    public int SchemaVersion { get; set; } = 2;

    /// <summary>
    /// Qdrant payload field used to group document chunks by history ID.
    /// Defaults to "adr_id". Set to "rfc_id", "decision_id", etc. for other projects.
    /// Read by GetHistory to build a collection-agnostic history filter.
    /// </summary>
    public string HistoryField { get; set; } = "adr_id";

    /// <summary>
    /// doc_kind value for main ADR files. Read from metadata-rules.yaml adr.adr_doc_kind.
    /// Null when the collection was ingested without an adr: section (non-ADR projects).
    /// Falls back to "adr_main" at runtime when null.
    /// </summary>
    public string? AdrDocKind { get; set; }

    /// <summary>
    /// doc_kind value for ADR amendment files. Read from metadata-rules.yaml adr.amendment_doc_kind.
    /// Null when the collection was ingested without an adr: section.
    /// Falls back to "adr_amendment" at runtime when null.
    /// </summary>
    public string? AmendmentDocKind { get; set; }

    /// <summary>Create a <see cref="RagConfigPayload"/> from a loaded <see cref="RagConfig"/>.</summary>
    public static RagConfigPayload From(RagConfig cfg, IEnumerable<GlossaryEntry>? glossaryEntries = null) =>
        new()
        {
            MaxTokens      = cfg.Chunker?.MaxTokens ?? 512,
            OverlapTokens  = cfg.Chunker?.OverlapTokens ?? 64,
            ScoreThreshold = cfg.Query?.ScoreThreshold ?? 0.30f,
            FetchK         = cfg.Query?.FetchK ?? 20,
            Weights        = cfg.Ranking?.Weights ?? [],
            GlossaryEntries = glossaryEntries?.ToList() ?? [],
            SchemaVersion  = 2,
            AdrDocKind     = cfg.MetadataRules.Adr?.AdrDocKind,
            AmendmentDocKind = cfg.MetadataRules.Adr?.AmendmentDocKind,
        };
}

/// <summary>
/// Serializable per-collection multilingual glossary entry.
/// <c>English</c> is the synonym group appended on match; <c>Patterns</c> are lowercase
/// non-English tokens (word-boundary-matched at query time).
/// Stored in <see cref="RagConfigPayload.GlossaryEntries"/>; mirrored from
/// <c>multilingual-glossary.yaml</c> by the batch ingest path.
/// </summary>
public sealed class GlossaryEntry
{
    public string English { get; set; } = string.Empty;
    public List<string> Patterns { get; set; } = [];

    public GlossaryEntry() { }

    public GlossaryEntry(string english, IEnumerable<string> patterns)
    {
        English = english;
        Patterns = patterns.ToList();
    }
}
