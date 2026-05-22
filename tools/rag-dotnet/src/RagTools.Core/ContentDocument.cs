namespace RagTools.Core;

/// <summary>
/// Represents a full document stored in Qdrant (doc_kind = "full_content").
/// One point per file — used by read_docs and list_adrs.
/// Point ID = DeterministicId.ForContent(collection, relPath).
/// </summary>
public sealed record ContentDocument(
    string RelPath,
    string DocKind,
    string? Bc,
    string? Title,
    string Content,
    DateTimeOffset IngestedAt,
    IDictionary<string, string>? Metadata = null);
