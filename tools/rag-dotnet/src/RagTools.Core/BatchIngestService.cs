using Microsoft.Extensions.Logging;
using RagTools.Core.Config;
using RagTools.Core.Ingest;

namespace RagTools.Core;

/// <summary>
/// One document inside a batch ingest request (already parsed out of the source ZIP).
/// Content is the raw markdown text — sanitization happens later in <see cref="IDocumentProcessor"/>.
/// </summary>
public sealed record BatchDocument(string RelPath, string Content);

/// <summary>
/// Request to enqueue a batch of pre-parsed documents into the ingest pipeline.
/// HTTP/ZIP concerns (Content-Type, body size, archive parsing) belong to the caller.
/// </summary>
public sealed record BatchIngestRequest(
    string Collection,
    IReadOnlyList<BatchDocument> Documents,
    MetadataRulesSection? BatchRules,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Enqueue-side facade for batch ingest. Returns the typed <see cref="BatchIngestOutcome"/>
/// defined under <c>RagTools.Core.Ingest</c> so HTTP and CLI callers share one mapping
/// (<c>BatchIngestOutcomeExtensions.ToActionResult</c> / <c>CliExitCodeMapper.ToExitCode</c>).
/// Owns the queue-capacity invariant only — ZIP parsing and HTTP validation belong elsewhere.
///
/// As part of P3-1 / P3-5 (ADR-0028), a successful enqueue also persists the effective
/// <see cref="RagConfigPayload"/> for the target collection to Qdrant and invalidates
/// the <see cref="IConfigSource"/> cache entry so the next query observes fresh settings.
/// </summary>
public interface IBatchIngestService
{
    Task<BatchIngestOutcome> EnqueueAsync(BatchIngestRequest request, CancellationToken ct = default);
}

/// <inheritdoc cref="IBatchIngestService"/>
public sealed class BatchIngestService(
    IngestChannel channel,
    OperationStore operations,
    IDocumentStore store,
    IConfigSource configSource,
    RagConfig cfg,
    ILogger<BatchIngestService> logger) : IBatchIngestService
{
    public async Task<BatchIngestOutcome> EnqueueAsync(BatchIngestRequest request, CancellationToken ct = default)
    {
        if (channel.PendingCount + request.Documents.Count > channel.Capacity)
        {
            logger.LogWarning(
                "Batch ingest: queue full — rejecting {Count} file(s) for {Collection} (pending={Pending}, capacity={Capacity})",
                request.Documents.Count, request.Collection, channel.PendingCount, channel.Capacity);

            return new BatchIngestOutcome.Failure(
                BatchIngestError.QueueFull,
                $"Ingest queue is full (pending={channel.PendingCount}, capacity={channel.Capacity}).",
                new Dictionary<string, object?>
                {
                    ["pending"]  = channel.PendingCount,
                    ["capacity"] = channel.Capacity,
                    ["incoming"] = request.Documents.Count,
                });
        }

        // P3-1/P3-5: persist effective config snapshot for this collection *before*
        // queuing jobs so downstream queries always observe a config matching the
        // ingested data. If persistence fails the batch is rejected — the
        // alternative (queue jobs, lose config) would leave the collection in an
        // inconsistent state for layered/qdrant config modes.
        var payload = BuildEffectivePayload(request.BatchRules);
        try
        {
            await store.StoreConfigAsync(request.Collection, payload, ct);
            configSource.Invalidate(request.Collection);
            logger.LogInformation(
                "RAG_PHASE3 stored config for collection={Collection} terms={Terms}",
                request.Collection, payload.GlossaryTerms.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Batch ingest: failed to persist config for {Collection} — rejecting batch",
                request.Collection);
            return new BatchIngestOutcome.Failure(
                BatchIngestError.ChannelWriteFailed,
                $"Failed to persist collection config: {ex.Message}",
                new Dictionary<string, object?> { ["collection"] = request.Collection });
        }

        var enqueuedAt = DateTimeOffset.UtcNow;
        var opList = new List<BatchOperationEntry>(request.Documents.Count);

        foreach (var doc in request.Documents)
        {
            ct.ThrowIfCancellationRequested();

            var safeRelPath = doc.RelPath.Replace('/', '-');
            var opId = $"{request.Collection}:{safeRelPath}:{enqueuedAt.Ticks}-{opList.Count}";

            var docKind = request.BatchRules is not null
                ? RagConfig.DetectDocKindFromRules(doc.RelPath, request.BatchRules) : null;
            var adrId = request.BatchRules is not null
                ? RagConfig.DetectAdrIdFromRules(doc.RelPath, request.BatchRules) : null;

            var job = new IngestJob
            {
                OperationId = opId,
                Collection  = request.Collection,
                RelPath     = doc.RelPath,
                Content     = doc.Content,
                DocKind     = docKind,
                AdrId       = adrId,
                EnqueuedAt  = enqueuedAt,
            };

            if (!channel.TryWrite(job))
            {
                logger.LogError(
                    "Batch ingest: channel write failed after capacity check (op={OpId})", opId);
                return new BatchIngestOutcome.Failure(
                    BatchIngestError.ChannelWriteFailed,
                    $"Failed to enqueue job after capacity check (op={opId}).",
                    new Dictionary<string, object?> { ["operationId"] = opId });
            }

            operations.MarkQueued(opId, request.Collection, doc.RelPath, enqueuedAt);

            // StatusUrl format mirrors the route defined in IngestController:
            //   GET /ingest/{collection}/operations/{opId}
            var statusUrl = $"/ingest/{request.Collection}/operations/{Uri.EscapeDataString(opId)}";
            opList.Add(new BatchOperationEntry(opId, doc.RelPath, statusUrl));
        }

        logger.LogInformation(
            "Batch ingest: queued {Count} job(s) for {Collection}", opList.Count, request.Collection);

        var batchId  = $"batch:{request.Collection}:{enqueuedAt.Ticks}";
        var response = new BatchIngestResponse(batchId, opList.Count, opList, request.Warnings);
        return new BatchIngestOutcome.Success(response);
    }

    // Compose the payload persisted with this batch.
    // Server defaults come from the mounted RagConfig; per-batch BatchRules (if any)
    // override only the adr.* doc_kind fields — they are the only doc-kind hints that
    // can legally vary between batches of the same collection.
    // Glossary persistence is wired in P3-3.
    private RagConfigPayload BuildEffectivePayload(MetadataRulesSection? batchRules)
    {
        var payload = RagConfigPayload.From(cfg);
        if (batchRules?.Adr is { } adr)
        {
            if (!string.IsNullOrWhiteSpace(adr.AdrDocKind))       payload.AdrDocKind       = adr.AdrDocKind;
            if (!string.IsNullOrWhiteSpace(adr.AmendmentDocKind)) payload.AmendmentDocKind = adr.AmendmentDocKind;
        }
        return payload;
    }
}
