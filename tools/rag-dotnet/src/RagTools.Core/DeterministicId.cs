using System.Security.Cryptography;
using System.Text;

namespace RagTools.Core;

/// <summary>
/// Computes deterministic GUIDs for Qdrant point IDs.
///
/// All IDs are derived from MD5(UTF-8 key) converted to a UUID (version 3 style).
/// Same input always produces the same GUID — safe for idempotent upserts.
///
/// Conventions:
///   Chunk point     → ForChunk(collection, relPath, chunkIndex)
///   Content point   → ForContent(collection, relPath)
///   Config point    → ForConfig(collection, docKind)  e.g. ForConfig("ecommerceapp", DocKind.Config)
///   Operation point → ForOperation(operationId)
/// </summary>
public static class DeterministicId
{
    /// <summary>Compute the Qdrant point ID for a chunk (doc_kind = "chunk").</summary>
    public static Guid ForChunk(string collection, string relPath, int chunkIndex) =>
        FromKey($"{collection}:{relPath}:{chunkIndex}");

    /// <summary>
    /// Compute the Qdrant point ID for a full-content point (doc_kind = "full_content").
    /// This is also stored as <c>content_id</c> in every chunk payload for O(1) batch fetch.
    /// </summary>
    public static Guid ForContent(string collection, string relPath) =>
        FromKey($"{collection}:{relPath}");

    /// <summary>
    /// Compute the Qdrant point ID for a per-collection metadata point
    /// (e.g. doc_kind = "__config__", "__glossary__", "__rules__", "__queries__").
    /// </summary>
    public static Guid ForConfig(string collection, string docKind) =>
        FromKey($"{collection}:{docKind}:v1");

    /// <summary>Compute the Qdrant point ID for an async ingest operation.</summary>
    public static Guid ForOperation(string operationId) =>
        FromKey($"__op__:{operationId}");

    // ── Private ───────────────────────────────────────────────────────────────

    private static Guid FromKey(string key)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(key));
        // Force version 3 (name-based MD5) and variant bits so it is a valid UUID.
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);  // version 3
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);  // variant RFC 4122
        return new Guid(hash);
    }
}
