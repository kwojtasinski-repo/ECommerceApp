using System.Collections.Concurrent;

namespace RagTools.Core;

/// <summary>
/// In-memory store for ingest operation status.
/// Tracks the lifecycle of each <see cref="IngestJob"/>: queued → processing → completed/failed.
///
/// Operations are retained for <see cref="RetentionPeriod"/> (default: 1 hour) then evicted.
/// This is intentionally in-memory — operations survive only as long as the server process.
/// Persistent tracking across restarts is deferred to ADR-0028 Step 6 (Qdrant-backed ops).
///
/// Registered as a singleton in DI.
/// </summary>
public sealed class OperationStore
{
    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, IngestOperationResult> _ops = new();

    // ── Write path (called by IngestWorker) ──────────────────────────────────

    /// <summary>Register a new operation in the Queued state.</summary>
    public void MarkQueued(string operationId, string collection, string relPath, DateTimeOffset enqueuedAt)
    {
        _ops[operationId] = new IngestOperationResult
        {
            OperationId = operationId,
            Collection  = collection,
            RelPath     = relPath,
            Status      = IngestStatus.Queued,
            EnqueuedAt  = enqueuedAt,
        };
    }

    /// <summary>Transition an operation to the Processing state.</summary>
    public void MarkProcessing(string operationId, string collection, string relPath, DateTimeOffset enqueuedAt)
    {
        _ops[operationId] = new IngestOperationResult
        {
            OperationId = operationId,
            Collection  = collection,
            RelPath     = relPath,
            Status      = IngestStatus.Processing,
            EnqueuedAt  = enqueuedAt,
            StartedAt   = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Transition an operation to the Completed state.</summary>
    public void MarkCompleted(string operationId, int chunkCount, string docKind = "")
    {
        if (_ops.TryGetValue(operationId, out var existing))
        {
            _ops[operationId] = existing with
            {
                Status      = IngestStatus.Completed,
                CompletedAt = DateTimeOffset.UtcNow,
                ChunkCount  = chunkCount,
                DocKind     = docKind,
            };
        }
    }

    /// <summary>Transition an operation to the Failed state.</summary>
    public void MarkFailed(string operationId, string errorMessage)
    {
        if (_ops.TryGetValue(operationId, out var existing))
        {
            _ops[operationId] = existing with
            {
                Status       = IngestStatus.Failed,
                CompletedAt  = DateTimeOffset.UtcNow,
                ErrorMessage = errorMessage,
            };
        }
    }

    // ── Read path (called by the HTTP controller) ─────────────────────────────

    /// <summary>Get the current status of an operation. Returns null if not found or expired.</summary>
    public IngestOperationResult? Get(string operationId)
    {
        if (!_ops.TryGetValue(operationId, out var op)) return null;

        // Evict expired entries on access.
        var age = DateTimeOffset.UtcNow - op.EnqueuedAt;
        if (age > RetentionPeriod)
        {
            _ops.TryRemove(operationId, out _);
            return null;
        }
        return op;
    }

    /// <summary>All operations for a given collection (non-expired).</summary>
    public IReadOnlyList<IngestOperationResult> GetByCollection(string collection)
    {
        var now = DateTimeOffset.UtcNow;
        return _ops.Values
            .Where(op => op.Collection == collection && (now - op.EnqueuedAt) <= RetentionPeriod)
            .OrderByDescending(op => op.EnqueuedAt)
            .ToList();
    }
}
