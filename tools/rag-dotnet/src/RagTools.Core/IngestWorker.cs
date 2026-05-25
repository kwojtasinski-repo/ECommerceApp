using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RagTools.Core.Primitives;

namespace RagTools.Core;

/// <summary>
/// Background service that processes <see cref="IngestJob"/> items from <see cref="IngestChannel"/>.
///
/// Lifecycle:
///   - Registered as a hosted service: services.AddHostedService&lt;IngestWorker&gt;()
///   - Reads jobs one at a time from the channel (single consumer — thread-safe by design)
///   - Delegates the full ingest pipeline to <see cref="IDocumentProcessor"/>
///   - Reports status transitions via OperationStore (queued → processing → completed/failed)
///   - Graceful shutdown: stops reading when CancellationToken is cancelled; in-flight job completes
///
/// HTTP path uses <c>EnsureCollection: true</c> (dynamic collections from batch ingest API)
/// and <c>StoreFullContent: true</c> (so read_docs can fall back from Qdrant).
/// </summary>
public sealed class IngestWorker(
    IngestChannel channel,
    IDocumentProcessor processor,
    OperationStore operations,
    ILogger<IngestWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("IngestWorker started, listening for jobs ...");

        await foreach (var job in channel.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessJobAsync(job, stoppingToken);
        }

        logger.LogInformation("IngestWorker stopped.");
    }

    private async Task ProcessJobAsync(IngestJob job, CancellationToken ct)
    {
        logger.LogDebug("IngestWorker: starting job {OperationId} ({RelPath})", job.OperationId, job.RelPath);
        operations.MarkProcessing(job.OperationId, job.Collection, job.RelPath, job.EnqueuedAt);

        try
        {
            var result = await processor.ProcessAsync(new DocumentProcessingRequest(
                Collection:       CollectionName.Parse(job.Collection),
                RelPath:          job.RelPath,
                Content:          job.Content,
                DocKindOverride:  job.DocKind,
                AdrIdOverride:    job.AdrId,
                EnsureCollection: true,
                StoreFullContent: true),
                ct);

            operations.MarkCompleted(job.OperationId, result.ChunkCount, result.DocKind ?? "");
            logger.LogInformation("IngestWorker: completed {RelPath} — {Chunks} chunk(s)",
                job.RelPath, result.ChunkCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "IngestWorker: failed to process {RelPath} (op={OperationId})",
                job.RelPath, job.OperationId);
            operations.MarkFailed(job.OperationId, ex.Message);
        }
    }
}
