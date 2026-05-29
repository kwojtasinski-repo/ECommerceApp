using Microsoft.Extensions.Logging;
using RagTools.Core.Config;
using RagTools.Core.Primitives;

namespace RagTools.Core;

/// <summary>
/// Request for processing a single document end-to-end.
/// </summary>
public sealed record DocumentProcessingRequest(
    CollectionName Collection,
    string RelPath,
    string Content,
    /// <summary>Override the auto-detected doc_kind (e.g. caller knows kind from manifest).</summary>
    string? DocKindOverride = null,
    /// <summary>Override the auto-detected ADR id.</summary>
    string? AdrIdOverride = null,
    /// <summary>File size in bytes (used by the stub-file weight rule). Defaults to <c>Content.Length</c>.</summary>
    int? FileSizeBytes = null,
    /// <summary>If true, <see cref="IDocumentStore.EnsureCollectionAsync"/> is called before upsert (HTTP path).</summary>
    bool EnsureCollection = false,
    /// <summary>If true, the full content is also stored via <see cref="IDocumentStore.StoreDocumentAsync"/> (HTTP path).</summary>
    bool StoreFullContent = false);

/// <summary>Outcome of <see cref="IDocumentProcessor.ProcessAsync"/>.</summary>
public sealed record ProcessingResult(
    int ChunkCount,
    string Title,
    string? DocKind,
    string? AdrId,
    float Weight);

/// <summary>
/// Single source of truth for the ingest pipeline:
/// sanitize → extract title → detect kind/adr → chunk → resolve weight → embed → upsert.
///
/// Replaces the two divergent copies in <see cref="FileIngestor"/> (CLI) and
/// <see cref="IngestWorker"/> (HTTP) which had silently drifted on
/// sanitization, title extraction, and weight resolution.
/// </summary>
public interface IDocumentProcessor
{
    Task<ProcessingResult> ProcessAsync(DocumentProcessingRequest request, CancellationToken ct = default);
}

public sealed class DocumentProcessor(
    RagConfig cfg,
    MarkdownChunker chunker,
    IEmbedder embedder,
    IDocumentStore store,
    IConfigSource configSource,
    ILogger<DocumentProcessor> logger) : IDocumentProcessor
{
    private const int EmbeddingBatchSize = 32;

    public async Task<ProcessingResult> ProcessAsync(
        DocumentProcessingRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var collection = request.Collection.Value;

        // Resolve per-collection config once for this document (ADR-0028 Phase 3 / P3-3b + P3-3c).
        // Chunker uses MaxTokens/OverlapTokens from here; weights resolved against payload.Weights.
        var payload = await configSource.GetEffectiveAsync(collection, ct);

        // 1. Sanitize input (U+FFFD → '?'). No-op if clean.
        var content = TextSanitizer.RemoveReplacementChars(request.Content, request.RelPath, logger);

        // 2. Title, kind, adr_id, weight.
        // ADR-0028 Phase 3 / P3-3c: weights resolved from the per-collection payload (Weights);
        // StubByteThreshold stays mounted-only — it's not persisted in RagConfigPayload.
        var title  = DocumentMetadata.ExtractTitle(content, request.RelPath);
        var kind   = request.DocKindOverride ?? cfg.DetectDocKind(request.RelPath);
        var adrId  = request.AdrIdOverride   ?? cfg.DetectAdrId(request.RelPath);
        var size   = request.FileSizeBytes ?? content.Length;
        var weight = RankingWeightResolver.Resolve(request.RelPath, size, payload.Weights, cfg.Ranking.StubByteThreshold);

        // 3. Chunk (heading-aware MarkdownChunker — per-collection MaxTokens/OverlapTokens).
        var chunks = chunker.Chunk(content, request.RelPath, payload.MaxTokens, payload.OverlapTokens);
        logger.LogDebug(
            "DocumentProcessor: {RelPath} → {Count} chunk(s), kind={Kind}, weight={Weight}, maxTokens={MaxTokens}",
            request.RelPath, chunks.Count, kind, weight, payload.MaxTokens);

        // 4. Ensure collection (HTTP path with dynamic collections).
        if (request.EnsureCollection)
        {
            await store.EnsureCollectionAsync(collection, embedder.Dimensions, ct);
        }

        // 5. Idempotent re-ingest: drop existing points for this path.
        await store.DeleteByPathsAsync(collection, [request.RelPath], ct);

        // 6. Embed in batches and build points.
        var points = new List<RagPoint>(chunks.Count);
        for (var i = 0; i < chunks.Count; i += EmbeddingBatchSize)
        {
            var batch   = chunks.Skip(i).Take(EmbeddingBatchSize).ToList();
            var texts   = batch.Select(c => c.Breadcrumb + "\n\n" + c.Text).ToList();
            var vectors = await embedder.EmbedBatchAsync(texts, ct);

            for (var j = 0; j < batch.Count; j++)
            {
                var chunk      = batch[j];
                var chunkIndex = i + j;
                var id         = ManifestService.StableId(request.RelPath, chunk.Breadcrumb, chunk.StartLine);
                var contentId  = DeterministicId.ForContent(collection, request.RelPath);

                points.Add(new RagPoint(id, vectors[j], new RagPayload(
                    RelPath:     request.RelPath,
                    DocTitle:    title,
                    DocKind:     kind,
                    AdrId:       adrId,
                    Breadcrumb:  chunk.Breadcrumb,
                    HeadingPath: chunk.HeadingPath,
                    StartLine:   chunk.StartLine,
                    EndLine:     chunk.EndLine,
                    TokenCount:  chunk.TokenCount,
                    Weight:      weight,
                    Text:        chunk.Text,
                    ChunkIndex:  chunkIndex,
                    ContentId:   contentId)));
            }
        }

        await store.UpsertChunksAsync(collection, points, ct);

        // 7. Optionally store full content for read_docs fallback (HTTP path).
        if (request.StoreFullContent)
        {
            var doc = new ContentDocument(
                RelPath:    request.RelPath,
                DocKind:    kind,
                Bc:         null,
                Title:      title,
                Content:    content,
                IngestedAt: DateTimeOffset.UtcNow);
            await store.StoreDocumentAsync(collection, doc, ct);
        }

        return new ProcessingResult(chunks.Count, title, kind, adrId, weight);
    }
}
