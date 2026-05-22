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

        // Build an operation ID that is safe to use in a URL path segment.
        // relPath can contain '/' (e.g. "docs/concepts/ddd.md"); embedding the raw relPath
        // in the URL caused .NET's Uri class to decode the percent-encoded %2F back to '/',
        // adding extra path segments and making the GET /operations/{opId} route return 404.
        // Replace '/' with '-' so the opId never contains a forward slash.
        var enqueuedAt = DateTimeOffset.UtcNow;
        var safeRelPath = request.RelPath.Replace('/', '-');
        var opId = $"{collection}:{safeRelPath}:{enqueuedAt.Ticks}";

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

    /// <summary>
    /// Upload a ZIP archive of documents for async ingestion.
    ///
    /// Body: raw <c>application/zip</c> bytes — each file in the archive is ingested as a separate job.
    /// Returns: 202 Accepted with a <see cref="BatchIngestResponse"/> listing all queued operations.
    /// Returns: 400 Bad Request when the body is not a valid ZIP or contains no files.
    /// Returns: 503 Service Unavailable when the ingest queue cannot fit all files.
    /// </summary>
    [HttpPost("/ingest/{collection}/batch")]
    public async Task<IActionResult> UploadBatch(string collection)
    {
        using var ms = new System.IO.MemoryStream();
    await Request.Body.CopyToAsync(ms);
    ms.Position = 0;

    System.IO.Compression.ZipArchive zip;
    try
    {
        zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true);
    }
    catch (Exception)
    {
        return BadRequest(new { error = "Invalid ZIP archive" });
    }

    var fileEntries = zip.Entries
        .Where(e => !e.FullName.EndsWith('/') && e.Length > 0)
        .ToList();

    if (fileEntries.Count == 0)
        return BadRequest(new { error = "ZIP contains no files" });

    if (channel.PendingCount + fileEntries.Count > channel.Capacity)
    {
        logger.LogWarning("Ingest queue full, rejecting batch of {Count} files for {Collection}", fileEntries.Count, collection);
        return StatusCode(503, new { error = "Ingest queue is full. Retry after a moment.", pending = channel.PendingCount });
    }

    var enqueuedAt = DateTimeOffset.UtcNow;
    var opList     = new List<BatchOperationEntry>(fileEntries.Count);

    foreach (var entry in fileEntries)
    {
        var relPath = entry.FullName;
        string content;
        using (var reader = new System.IO.StreamReader(entry.Open()))
            content = await reader.ReadToEndAsync();

        var safeRelPath = relPath.Replace('/', '-');
        var opId = $"{collection}:{safeRelPath}:{enqueuedAt.Ticks}-{opList.Count}";

        var job = new IngestJob
        {
            OperationId = opId,
            Collection  = collection,
            RelPath     = relPath,
            Content     = content,
            EnqueuedAt  = enqueuedAt,
        };

        channel.TryWrite(job);
        operations.MarkQueued(opId, collection, relPath, enqueuedAt);

        var statusUrl = $"/ingest/{collection}/operations/{Uri.EscapeDataString(opId)}";
        opList.Add(new BatchOperationEntry { RelPath = relPath, OperationId = opId, StatusUrl = statusUrl });
    }

    logger.LogInformation("Batch queued {Count} jobs for {Collection}", opList.Count, collection);

    return Accepted(new BatchIngestResponse
    {
        BatchId    = $"batch:{collection}:{enqueuedAt.Ticks}",
        Count      = opList.Count,
        Operations = opList,
    });
}
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

/// <summary>Response body for 202 Accepted from POST /ingest/{collection}/batch.</summary>
public sealed class BatchIngestResponse
{
    public string                    BatchId    { get; set; } = string.Empty;
    public int                       Count      { get; set; }
    public List<BatchOperationEntry> Operations { get; set; } = [];
}

/// <summary>Per-file entry within <see cref="BatchIngestResponse"/>.</summary>
public sealed class BatchOperationEntry
{
    public string OperationId { get; set; } = string.Empty;
    public string RelPath     { get; set; } = string.Empty;
    public string StatusUrl   { get; set; } = string.Empty;
}
