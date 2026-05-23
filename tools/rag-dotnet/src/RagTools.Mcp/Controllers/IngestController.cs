using Microsoft.AspNetCore.Mvc;
using RagTools.Core;

namespace RagTools.Mcp.Controllers;

/// <summary>
/// Ingest controller — accepts document uploads and queues them for async processing.
///
/// Endpoints:
///   POST   /ingest/{collection}/batch                     — upload a ZIP of documents
///   GET    /ingest/{collection}/operations/{opId}         — poll operation status
///   GET    /ingest/{collection}/operations                — list all operations for collection
///   GET    /admin/stats                                   — queue depth + worker health
///
/// Authentication: all /ingest/* routes require X-Api-Key header (ApiKeyMiddleware).
/// </summary>
[ApiController]
public sealed class IngestController(
    IngestChannel channel,
    OperationStore operations,
    ILogger<IngestController> logger) : ControllerBase
{
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
