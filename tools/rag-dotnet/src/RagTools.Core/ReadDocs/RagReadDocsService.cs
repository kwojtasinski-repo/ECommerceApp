using Microsoft.Extensions.Logging;
using RagTools.Core.Config;
using RagTools.Core.ContentSources;
using RagTools.Core.Shared;

namespace RagTools.Core.ReadDocs;

/// <summary>
/// Default <see cref="IRagReadDocsService"/> — extracted from
/// <c>RagTools.Mcp.Tools.RagTools.ReadDocs</c> so MCP, HTTP, and CLI share one pipeline.
///
/// Reads <see cref="RagConfigPayload.ScoreThreshold"/> and <see cref="RagConfigPayload.Weights"/>
/// from <see cref="IConfigSource"/> so per-collection overrides (ADR-0028, P3-1) are honoured.
/// </summary>
public sealed class RagReadDocsService(
    IEmbedder embedder,
    IDocumentStore store,
    IContentSource contentSource,
    IConfigSource configSource,
    ILogger<RagReadDocsService> logger) : IRagReadDocsService
{
    public const int MaxTopFiles = 5;

    private static readonly System.Text.RegularExpressions.Regex FullIntentRe = new(
        @"\b(all details|full details|full content|full text|entire|whole file|show me all|explain everything|everything about|complete picture|all about|deep dive|in full|from start to finish)\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    public async Task<ReadDocsOutcome> ReadAsync(ReadDocsRequest request, CancellationToken ct = default)
    {
        if (Validate(request) is { } validationFailure)
            return validationFailure;

        var fullMode = FullIntentRe.IsMatch(request.Question);
        logger.LogDebug(
            "ReadAsync: collection={Collection} question={Question} topic={Topic} topFiles={TopFiles} fullMode={FullMode}",
            request.Collection, request.Question, request.Topic, request.TopFiles, fullMode);

        var payload = await configSource.GetEffectiveAsync(request.Collection, ct);

        var embedResult = await EmbedAsync(request.Question, ct);
        if (embedResult.Failure is not null) return embedResult.Failure;

        var searchResult = await SearchAsync(request.Collection, embedResult.Vector!, request.TopFiles, payload.ScoreThreshold, ct);
        if (searchResult.Failure is not null) return searchResult.Failure;

        var ranked = RankFiles(searchResult.Hits!, request, payload.Weights);

        var files = new List<ReadDocsFile>(ranked.Count);
        foreach (var f in ranked)
        {
            if (fullMode)
            {
                var (file, failure) = await BuildFullModeFileAsync(request.Collection, f, ct);
                if (failure is not null) return failure;
                files.Add(file!);
            }
            else
            {
                files.Add(BuildChunksModeFile(f));
            }
        }

        logger.LogInformation("ReadAsync: returned {Count} file(s) for '{Question}'", files.Count, request.Question);
        return new ReadDocsOutcome.Success(new ReadDocsResponse(
            Collection: request.Collection,
            Question: request.Question,
            Mode: fullMode ? ReadDocsMode.Full : ReadDocsMode.Chunks,
            Files: files));
    }

    private static ReadDocsOutcome.Failure? Validate(ReadDocsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return new ReadDocsOutcome.Failure(ReadDocsError.EmptyQuestion, "Question must not be empty or whitespace.");

        if (request.TopFiles < 1 || request.TopFiles > MaxTopFiles)
            return new ReadDocsOutcome.Failure(
                ReadDocsError.TopFilesOutOfRange,
                $"top_files={request.TopFiles} is out of range [1, {MaxTopFiles}].",
                new Dictionary<string, object?> { ["topFiles"] = request.TopFiles, ["max"] = MaxTopFiles });

        return null;
    }

    private async Task<(float[]? Vector, ReadDocsOutcome.Failure? Failure)> EmbedAsync(string question, CancellationToken ct)
    {
        try { return (await embedder.EmbedAsync(question, ct), null); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ReadAsync: embedder failed");
            return (null, new ReadDocsOutcome.Failure(ReadDocsError.EmbeddingFailed, $"Embedder failed: {ex.Message}"));
        }
    }

    private async Task<(IReadOnlyList<DocumentSearchResult>? Hits, ReadDocsOutcome.Failure? Failure)> SearchAsync(
        string collection, float[] queryVec, int topFiles, float scoreThreshold, CancellationToken ct)
    {
        try
        {
            var opts = new SearchOptions(Math.Max(30, topFiles * 15), scoreThreshold);
            return (await store.SearchAsync(collection, queryVec, opts, ct), null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ReadAsync: store search failed for {Collection}", collection);
            return (null, new ReadDocsOutcome.Failure(
                ReadDocsError.StoreSearchFailed,
                $"Document store search failed: {ex.Message}",
                new Dictionary<string, object?> { ["collection"] = collection }));
        }
    }

    private List<RankedFile> RankFiles(IReadOnlyList<DocumentSearchResult> rawHits, ReadDocsRequest request, IReadOnlyList<WeightEntry> weights)
    {
        var weighted = TopicFilter.ApplyWeights(rawHits, weights);
        var filtered = request.Topic is not null
            ? weighted.Where(h => TopicFilter.Matches(h, request.Topic))
            : weighted;

        return filtered
            .GroupBy(h => h.RelPath)
            .Select(g => new RankedFile(g.Key, g.Max(h => h.Score), g.OrderByDescending(h => h.Score).ToList()))
            .OrderByDescending(x => x.Best)
            .Take(request.TopFiles)
            .ToList();
    }

    private async Task<(ReadDocsFile? File, ReadDocsOutcome.Failure? Failure)> BuildFullModeFileAsync(
        string collection, RankedFile f, CancellationToken ct)
    {
        string? body;
        try { body = await contentSource.ReadAsync(collection, f.RelPath, ct); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ReadAsync: content fetch failed for {RelPath}", f.RelPath);
            return (null, new ReadDocsOutcome.Failure(
                ReadDocsError.ContentFetchFailed,
                $"Content fetch failed for '{f.RelPath}': {ex.Message}",
                new Dictionary<string, object?> { ["relPath"] = f.RelPath }));
        }

        return (new ReadDocsFile(
            RelPath: f.RelPath,
            Score: Math.Round(f.Best, 3),
            DocKind: f.Chunks[0].DocKind,
            Mode: ReadDocsMode.Full,
            Content: body ?? $"[Content unavailable for {f.RelPath}]",
            Chunks: Array.Empty<ReadDocsChunk>()), null);
    }

    private static ReadDocsFile BuildChunksModeFile(RankedFile f)
    {
        var chunks = f.Chunks.Take(8).Select((c, i) => new ReadDocsChunk(
            Rank: i + 1,
            Score: Math.Round(c.Score, 3),
            StartLine: c.StartLine,
            Text: c.Text)).ToList();

        return new ReadDocsFile(
            RelPath: f.RelPath,
            Score: Math.Round(f.Best, 3),
            DocKind: f.Chunks[0].DocKind,
            Mode: ReadDocsMode.Chunks,
            Content: null,
            Chunks: chunks);
    }

    private sealed record RankedFile(string RelPath, double Best, IReadOnlyList<DocumentSearchResult> Chunks);
}
