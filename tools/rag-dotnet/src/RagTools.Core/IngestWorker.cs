using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RagTools.Core;

/// <summary>
/// Background service that processes <see cref="IngestJob"/> items from <see cref="IngestChannel"/>.
///
/// Lifecycle:
///   - Registered as a hosted service: services.AddHostedService&lt;IngestWorker&gt;()
///   - Reads jobs one at a time from the channel (single consumer — thread-safe by design)
///   - For each job: chunk → embed → upsert into IDocumentStore
///   - Reports status transitions via OperationStore (queued → processing → completed/failed)
///   - Graceful shutdown: stops reading when CancellationToken is cancelled; in-flight job completes
///
/// The worker does NOT manage the Qdrant collection (EnsureCollectionAsync is called at startup
/// by the host). Ingest jobs assume the collection already exists.
/// </summary>
public sealed class IngestWorker(
    IngestChannel channel,
    IDocumentStore store,
    OnnxEmbedder embedder,
    RagConfig cfg,
    OperationStore operations,
    ILogger<IngestWorker> logger,
    ITokenCounter? tokenCounter = null) : BackgroundService
{
    private readonly MarkdownChunker _chunker = new(cfg.Chunker,
        tokenCounter ?? SentencePieceTokenCounter.FromModelDir(
            Path.Combine(AppContext.BaseDirectory, "model")));

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
            // 1. Detect doc kind (use override if provided, else auto-detect from path).
            var kind  = job.DocKind ?? cfg.DetectDocKind(job.RelPath);
            var adrId = cfg.DetectAdrId(job.RelPath);
            var title = ExtractTitle(job.Content, job.RelPath);

            // 2. Chunk the document.
            var chunks = _chunker.Chunk(job.Content, job.RelPath);
            logger.LogDebug("IngestWorker: {RelPath} — {Count} chunk(s), kind={Kind}", job.RelPath, chunks.Count, kind);

            // 3. Delete existing points for this path (idempotent re-ingest).
            await store.DeleteByPathsAsync(job.Collection, [job.RelPath], ct);

            // 4. Embed in batches and upsert.
            var batchSize = cfg.Embedder.BatchSize;
            var points = new List<RagPoint>(chunks.Count);

            for (var i = 0; i < chunks.Count; i += batchSize)
            {
                var batch  = chunks.Skip(i).Take(batchSize).ToList();
                var texts  = batch.Select(c => c.Breadcrumb + "\n\n" + c.Text).ToList();
                var vectors = embedder.EmbedBatch(texts);

                for (var j = 0; j < batch.Count; j++)
                {
                    var chunk      = batch[j];
                    var chunkIndex = i + j;
                    var id         = ManifestService.StableId(job.RelPath, chunk.Breadcrumb, chunk.StartLine);
                    var contentId  = DeterministicId.ForContent(job.Collection, job.RelPath);

                    points.Add(new RagPoint(id, vectors[j], new RagPayload(
                        RelPath:     job.RelPath,
                        DocTitle:    title,
                        DocKind:     kind,
                        AdrId:       adrId,
                        Breadcrumb:  chunk.Breadcrumb,
                        HeadingPath: chunk.HeadingPath,
                        StartLine:   chunk.StartLine,
                        EndLine:     chunk.EndLine,
                        TokenCount:  chunk.TokenCount,
                        Weight:      1.0f,
                        Text:        chunk.Text,
                        ChunkIndex:  chunkIndex,
                        ContentId:   contentId)));
                }
            }

            await store.UpsertChunksAsync(job.Collection, points, ct);

            // 5. Store full-content point so read_docs can fall back from Qdrant.
            var contentDoc = new ContentDocument(
                RelPath:    job.RelPath,
                DocKind:    kind,
                Bc:         null,
                Title:      title,
                Content:    job.Content,
                IngestedAt: DateTimeOffset.UtcNow);

            await store.StoreDocumentAsync(job.Collection, contentDoc, ct);

            operations.MarkCompleted(job.OperationId, chunks.Count, kind);
            logger.LogInformation("IngestWorker: completed {RelPath} — {Chunks} chunk(s)", job.RelPath, chunks.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "IngestWorker: failed to process {RelPath} (op={OperationId})", job.RelPath, job.OperationId);
            operations.MarkFailed(job.OperationId, ex.Message);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ExtractTitle(string text, string relPath)
    {
        foreach (var line in text.Split('\n'))
        {
            var s = line.Trim();
            if (s.StartsWith("# ")) return s[2..].Trim();
            if (!string.IsNullOrEmpty(s) && !s.StartsWith('#') && !s.StartsWith("---")) break;
        }
        return Path.GetFileNameWithoutExtension(relPath);
    }
}
