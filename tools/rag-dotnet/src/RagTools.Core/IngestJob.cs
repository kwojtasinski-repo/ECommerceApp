namespace RagTools.Core;

/// <summary>
/// Represents a single document to be ingested into a RAG collection.
/// Created by the ingest controller and queued into <see cref="IngestChannel"/>.
/// </summary>
public sealed class IngestJob
{
    /// <summary>
    /// Unique operation ID. Returned to the caller so they can poll for status.
    /// Format: {collection}:{relPath}:{timestamp-ticks} — stable for idempotent re-queues.
    /// </summary>
    public required string OperationId { get; init; }

    /// <summary>Qdrant collection / project identifier.</summary>
    public required string Collection { get; init; }

    /// <summary>Relative path of the document within the project workspace (e.g. "docs/adr/0028/0028-remote-rag.md").</summary>
    public required string RelPath { get; init; }

    /// <summary>Full text content of the document (already read from upload or source).</summary>
    public required string Content { get; init; }

    /// <summary>
    /// Optional doc_kind override. When null, the worker auto-detects from the path
    /// using the same rules as the CLI ingest (<see cref="RagConfig.DetectDocKind"/>).
    /// </summary>
    public string? DocKind { get; init; }

    /// <summary>When the job was enqueued — used to compute operation latency.</summary>
    public DateTimeOffset EnqueuedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Possible states of an ingest operation tracked by <see cref="OperationStore"/>.
/// </summary>
public enum IngestStatus
{
    Queued,
    Processing,
    Completed,
    Failed,
}

/// <summary>
/// Snapshot of an ingest operation's state.
/// Returned by GET /ingest/{collection}/operations/{id}.
/// </summary>
public sealed record IngestOperationResult
{
    public required string OperationId { get; init; }
    public required string Collection  { get; init; }
    public required string RelPath     { get; init; }
    public required IngestStatus Status { get; init; }
    public required DateTimeOffset EnqueuedAt { get; init; }
    public DateTimeOffset? StartedAt   { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public int? ChunkCount            { get; init; }
    public string? ErrorMessage       { get; init; }
}
