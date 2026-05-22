using Microsoft.AspNetCore.Mvc;
using RagTools.Core;

namespace RagTools.Mcp.Controllers;

/// <summary>
/// Ingest controller — accepts document uploads and queues them for async processing.
///
/// Endpoints:
///   POST   /ingest/{collection}                        — upload a single document
///   GET    /ingest/{collection}/operations/{opId}      — poll operation status
///   GET    /ingest/{collection}/operations             — list all operations for collection
///   GET    /admin/stats                               — queue depth + worker health
///
/// Authentication: all /ingest/* routes require X-Api-Key header (ApiKeyMiddleware).
/// </summary>
[ApiController]
public sealed class IngestController(
    IngestChannel channel,
    OperationStore operations,
    ILogger<IngestController> logger) : ControllerBase
{
    /// <summary>
    /// Upload a single document for async ingestion.
    ///
    /// Body: <see cref="IngestRequest"/>
    /// Returns: 202 Accepted with operation location header.
    /// Returns: 503 Service Unavailable when the ingest queue is full.
    /// </summary>
    [HttpPost("/ingest/{collection}")]
    public IActionResult Ingest(string collection, [FromBody] IngestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RelPath))
            return BadRequest(new { error = "rel_path is required" });

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = "content is required" });

        // Build a deterministic operation ID so re-uploads of the same file are idempotent.
        var enqueuedAt = DateTimeOffset.UtcNow;
        var opId = $"{collection}:{request.RelPath}:{enqueuedAt.Ticks}";

        var job = new IngestJob
        {
            OperationId = opId,
            Collection  = collection,
            RelPath     = request.RelPath,
            Content     = request.Content,
            DocKind     = request.DocKind,
            EnqueuedAt  = enqueuedAt,
        };

        if (!channel.TryWrite(job))
        {
            logger.LogWarning("Ingest queue full, rejecting {RelPath} for {Collection}", request.RelPath, collection);
            return StatusCode(503, new { error = "Ingest queue is full. Retry after a moment.", pending = channel.PendingCount });
        }

        operations.MarkQueued(opId, collection, request.RelPath, enqueuedAt);
        logger.LogInformation("Queued ingest job {OperationId} for {Collection}/{RelPath}", opId, collection, request.RelPath);

        var location = $"/ingest/{collection}/operations/{Uri.EscapeDataString(opId)}";
        Response.Headers["Location"] = location;

        return Accepted(new IngestResponse
        {
            OperationId = opId,
            Collection  = collection,
            RelPath     = request.RelPath,
            StatusUrl   = location,
        });
    }

    /// <summary>Poll the status of an ingest operation.</summary>
    [HttpGet("/ingest/{collection}/operations/{opId}")]
    public IActionResult GetOperation(string collection, string opId)
    {
        var op = operations.Get(opId);
        if (op is null || op.Collection != collection)
            return NotFound(new { error = $"Operation {opId} not found in collection {collection}" });

        return Ok(op);
    }

    /// <summary>List all recent operations for a collection.</summary>
    [HttpGet("/ingest/{collection}/operations")]
    public IActionResult ListOperations(string collection) =>
        Ok(operations.GetByCollection(collection));

    /// <summary>Queue depth and basic worker health.</summary>
    [HttpGet("/admin/stats")]
    public IActionResult Stats() => Ok(new
    {
        queue_depth    = channel.PendingCount,
        retention_hours = OperationStore.RetentionPeriod.TotalHours,
    });
}

/// <summary>Request body for POST /ingest/{collection}.</summary>
public sealed class IngestRequest
{
    /// <summary>Relative path within the project (e.g. "docs/adr/0028/0028-remote-rag.md").</summary>
    public string RelPath { get; set; } = string.Empty;

    /// <summary>Full text content of the document.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Optional doc_kind override; auto-detected from path when null.</summary>
    public string? DocKind { get; set; }
}

/// <summary>Response body for 202 Accepted from POST /ingest/{collection}.</summary>
public sealed class IngestResponse
{
    public string OperationId { get; set; } = string.Empty;
    public string Collection  { get; set; } = string.Empty;
    public string RelPath     { get; set; } = string.Empty;
    public string StatusUrl   { get; set; } = string.Empty;
}
