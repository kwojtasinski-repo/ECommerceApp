using Microsoft.AspNetCore.Mvc;
using RagTools.Core;
using RagTools.Core.Ingest;
using RagTools.Mcp.Filters;
using RagTools.Mcp.Ingest;

namespace RagTools.Mcp.Controllers;

/// <summary>
/// Ingest controller — accepts document uploads and queues them for async processing.
///
/// Endpoints:
///   POST   /ingest/{collection:collection}/batch          — upload a ZIP of documents
///   GET    /ingest/{collection:collection}/operations/{opId} — poll operation status
///   GET    /ingest/{collection:collection}/operations     — list all operations for collection
///   GET    /admin/stats                                   — queue depth + worker health
///
/// The <c>:collection</c> route constraint (<see cref="Routing.CollectionNameRouteConstraint"/>)
/// rejects malformed names at routing time, so the action never sees them.
///
/// Authentication: all /ingest/* routes require X-Api-Key header (ApiKeyMiddleware).
/// </summary>
[ApiController]
public sealed class IngestController(
    IZipBatchParser parser,
    IBatchIngestService batchIngest,
    IngestChannel channel,
    OperationStore operations) : ControllerBase
{
    /// <summary>Poll the status of an ingest operation.</summary>
    [HttpGet("/ingest/{collection:collection}/operations/{opId}")]
    public IActionResult GetOperation(string collection, string opId)
    {
        var op = operations.Get(opId);
        if (op is null || op.Collection != collection)
        {
            return NotFound(new { error = $"Operation {opId} not found in collection {collection}" });
        }

        return Ok(op);
    }

    /// <summary>List all recent operations for a collection.</summary>
    [HttpGet("/ingest/{collection:collection}/operations")]
    public IActionResult ListOperations(string collection) =>
        Ok(operations.GetByCollection(collection));

    /// <summary>Queue depth and basic worker health.</summary>
    [HttpGet("/admin/stats")]
    public IActionResult Stats() => Ok(new
    {
        queue_depth     = channel.PendingCount,
        retention_hours = OperationStore.RetentionPeriod.TotalHours,
    });

    /// <summary>
    /// Upload a ZIP archive of documents for async ingestion.
    ///
    /// Pipeline: <see cref="ZipUploadFilter"/> (Content-Type / size guard) →
    /// <see cref="IZipBatchParser"/> (temp-file ZIP read + <see cref="BatchValidator"/>) →
    /// <see cref="IBatchIngestService"/> (capacity check + enqueue) →
    /// <see cref="BatchIngestOutcomeExtensions.ToActionResult"/> (202 / 4xx / 5xx).
    ///
    /// Status mapping is centralised — see <see cref="BatchIngestOutcomeExtensions.StatusFor"/>.
    /// </summary>
    [HttpPost("/ingest/{collection:collection}/batch")]
    [ZipUploadFilter]
    [RequestSizeLimit(ZipUploadFilter.DefaultMaxBytes)]
    public async Task<IActionResult> UploadBatch(string collection, CancellationToken ct)
    {
        var parseOutcome = await parser.ParseAsync(Request.Body, ct);
        return parseOutcome switch
        {
            ZipParseOutcome.Failure failure => failure.ToActionResult(),
            ZipParseOutcome.Success success => batchIngest.Enqueue(
                new BatchIngestRequest(collection, success.Batch.Documents, success.Batch.BatchRules, success.Batch.Warnings),
                ct).ToActionResult(),
            _ => throw new InvalidOperationException($"Unhandled ZipParseOutcome variant: {parseOutcome.GetType().Name}"),
        };
    }
}
