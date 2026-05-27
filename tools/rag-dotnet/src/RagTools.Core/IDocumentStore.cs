namespace RagTools.Core;

/// <summary>
/// Summary of an ADR for list_adrs responses.
/// </summary>
public sealed record AdrSummary(
    string Id,
    string Title,
    string MainFile,
    int Amendments,
    int Examples);

/// <summary>
/// Options passed to IDocumentStore.SearchAsync.
/// </summary>
public sealed record SearchOptions(
    int TopK,
    float ScoreThreshold,
    string? DocKindFilter = null,
    string? AdrIdFilter = null,
    /// <summary>
    /// Generic history filter: (fieldName, value).  When set, only chunks whose Qdrant
    /// payload has <c>fieldName == value</c> are returned.  Used by GetHistory to filter
    /// by the collection-configured history field (default "adr_id").
    /// </summary>
    (string Field, string Value)? HistoryFieldFilter = null);

/// <summary>
/// A single search result returned by IDocumentStore.SearchAsync.
/// Mirrors SearchHit — defined separately so IDocumentStore has no dependency on QdrantStore.
/// </summary>
public sealed record DocumentSearchResult(
    float Score,
    string RelPath,
    string DocTitle,
    string DocKind,
    string? AdrId,
    string Breadcrumb,
    int StartLine,
    int EndLine,
    string Text);

/// <summary>
/// Storage abstraction for RAG data. Tools and ingest workers must only access
/// Qdrant (or any other backend) through this interface.
///
/// All methods accept a <paramref name="collection"/> parameter — the Qdrant collection
/// name / project identifier derived from rag-config.yaml.
/// </summary>
public interface IDocumentStore : IDisposable
{
    // ── Ingest path ───────────────────────────────────────────────────────────

    /// <summary>Upsert a batch of chunk points. Idempotent — same ID is overwritten.</summary>
    Task UpsertChunksAsync(string collection, IReadOnlyList<RagPoint> chunks, CancellationToken ct = default);

    /// <summary>
    /// Upsert a full-content point (doc_kind = full_content, zero vector).
    /// Point ID = DeterministicId.ForContent(collection, relPath).
    /// </summary>
    Task StoreDocumentAsync(string collection, ContentDocument doc, CancellationToken ct = default);

    /// <summary>
    /// Store config, glossary, and query settings for a collection.
    /// Parsed from YAML at upload time — stored as structured JSON.
    /// Point ID = DeterministicId.ForConfig(collection, DocKind.Config).
    /// </summary>
    Task StoreConfigAsync(string collection, RagConfigPayload config, CancellationToken ct = default);

    /// <summary>Fetch the stored config for a collection. Returns null if not yet stored.</summary>
    Task<RagConfigPayload?> FetchConfigAsync(string collection, CancellationToken ct = default);

    /// <summary>Delete all points whose rel_path matches any of the given paths.</summary>
    Task DeleteByPathsAsync(string collection, IEnumerable<string> relPaths, CancellationToken ct = default);

    // ── Query path ────────────────────────────────────────────────────────────

    /// <summary>Semantic search. Results are ordered by descending score.</summary>
    Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(
        string collection, float[] queryVector, SearchOptions opts, CancellationToken ct = default);

    /// <summary>
    /// Fetch a full ContentDocument by relative path.
    /// Returns null if no content point exists for the path.
    /// </summary>
    Task<ContentDocument?> FetchContentAsync(string collection, string relPath, CancellationToken ct = default);

    /// <summary>List all ADRs indexed in this collection.</summary>
    Task<IReadOnlyList<AdrSummary>> ListAdrsAsync(string collection, CancellationToken ct = default);

    // ── Collection lifecycle ──────────────────────────────────────────────────

    /// <summary>Ensure the collection exists with the given vector dimensions.</summary>
    Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default);

    /// <summary>Drop and recreate the collection (--force-full ingest).</summary>
    Task RecreateCollectionAsync(string collection, int dimensions, CancellationToken ct = default);
}
