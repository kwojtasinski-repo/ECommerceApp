namespace RagTools.Core.Query;

/// <summary>
/// Exhaustive list of expected query failure modes.
/// Mirrors the <see cref="RagTools.Core.Ingest.BatchIngestError"/> pattern.
/// Status / exit-code mapping is owned by adapters (HTTP, MCP, CLI) — not by this enum.
/// </summary>
public enum QueryError
{
    EmptyQuestion,
    TopKOutOfRange,
    UnknownCollection,
    EmbeddingFailed,
    StoreSearchFailed,
    PostprocessorFailed,
}
