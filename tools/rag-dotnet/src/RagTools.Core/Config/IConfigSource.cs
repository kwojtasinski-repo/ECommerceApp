namespace RagTools.Core.Config;

/// <summary>
/// Resolves the effective query-time config for a collection.
///
/// Returns <see cref="RagConfigPayload"/> (the structured slice consumed by the query path:
/// score threshold, fetchK, weights, glossary terms, ADR doc-kinds, history field) — NOT
/// the full <see cref="RagConfig"/> which still carries file paths, workspace, embedder/chunker
/// settings that are ingest-time concerns and orthogonal to per-collection overrides.
///
/// Transport-aware implementations:
///   <see cref="FileConfigSource"/>     — STDIO mode default; returns mounted YAML only.
///   <see cref="QdrantConfigSource"/>   — pure Qdrant fetch; no mounted fallback (edge case).
///   <see cref="LayeredConfigSource"/>  — HTTP mode default; merges mounted defaults with
///                                       per-collection Qdrant override (override wins per-field).
///   <see cref="CachingConfigSource"/>  — decorator over any inner source, backed by
///                                       <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>
///                                       so the underlying store (in-memory today, Redis tomorrow)
///                                       is swappable with one DI line.
///
/// Wiring: see Program.cs — MCP_TRANSPORT + RAG_CONFIG_SOURCE env vars select the implementation.
/// </summary>
public interface IConfigSource
{
    /// <summary>
    /// Returns the effective <see cref="RagConfigPayload"/> for <paramref name="collection"/>.
    /// Implementations must never return null — fall back to mounted defaults if nothing is stored.
    /// </summary>
    ValueTask<RagConfigPayload> GetEffectiveAsync(string collection, CancellationToken ct = default);

    /// <summary>
    /// Drop any cached state for <paramref name="collection"/>. Called by ingest after
    /// a new config is persisted so the next query observes it immediately.
    /// Implementations without a cache may treat this as a no-op.
    /// </summary>
    void Invalidate(string collection);
}
