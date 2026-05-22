namespace RagTools.Core;

/// <summary>
/// Reserved doc_kind values used in Qdrant payloads.
/// All search queries must exclude kinds prefixed with "__" and "full_content".
/// </summary>
public static class DocKind
{
    // Chunk points (vector-searchable — the main ingest output)
    public const string Chunk = "chunk";

    // Full content points (zero vector — fetched by ID after search)
    public const string FullContent = "full_content";

    // Config and metadata stored per-collection (zero vector)
    public const string Config   = "__config__";
    public const string Glossary = "__glossary__";
    public const string Rules    = "__rules__";
    public const string Queries  = "__queries__";

    // Async ingest operations (stored in __ops__ collection)
    public const string Operation = "__op__";

    /// <summary>Returns true for internal doc_kind values that must never appear in search results.</summary>
    public static bool IsInternal(string? kind) =>
        kind is not null && (kind.StartsWith("__") || kind == FullContent);
}
