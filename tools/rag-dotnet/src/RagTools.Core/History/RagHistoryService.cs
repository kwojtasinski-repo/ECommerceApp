using Microsoft.Extensions.Logging;
using RagTools.Core.Config;

namespace RagTools.Core.History;

/// <summary>
/// Default <see cref="IRagHistoryService"/> — extracted from
/// <c>RagTools.Mcp.Tools.RagTools.GetHistory</c>.
/// </summary>
public sealed class RagHistoryService(
    IEmbedder embedder,
    IDocumentStore store,
    IConfigSource configSource,
    ILogger<RagHistoryService> logger) : IRagHistoryService
{
    public const string DefaultHistoryField = "adr_id";

    public async Task<HistoryOutcome> GetAsync(HistoryRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Id))
            return new HistoryOutcome.Failure(HistoryError.EmptyId, "id must not be empty or whitespace.");

        var historyField = await ResolveHistoryFieldAsync(request.Collection, ct);

        var embedResult = await EmbedAsync(request.Id, ct);
        if (embedResult.Failure is not null) return embedResult.Failure;

        var searchResult = await SearchAsync(request.Collection, embedResult.Vector!, historyField, request.Id, ct);
        if (searchResult.Failure is not null) return searchResult.Failure;

        var ordered = searchResult.Hits!
            .OrderBy(h => h.StartLine)
            .Select(h => new HistoryChunk(
                RelPath: h.RelPath,
                Breadcrumb: h.Breadcrumb,
                DocKind: h.DocKind,
                StartLine: h.StartLine,
                Text: h.Text))
            .ToList();

        logger.LogInformation(
            "GetAsync: {Field}={Id} returned {Count} chunks", historyField, request.Id, ordered.Count);

        return new HistoryOutcome.Success(new HistoryResponse(
            Id: request.Id,
            HistoryField: historyField,
            Chunks: ordered));
    }

    private async Task<string> ResolveHistoryFieldAsync(string collection, CancellationToken ct)
    {
        // ADR-0028 Phase 3 / P3-X: route through IConfigSource (cached + mode-switched) instead
        // of calling store.FetchConfigAsync directly. The mounted fallback (FileConfigSource)
        // returns the configured default, so the empty-string branch below is purely defensive.
        try
        {
            var payload = await configSource.GetEffectiveAsync(collection, ct);
            return !string.IsNullOrEmpty(payload.HistoryField)
                ? payload.HistoryField
                : DefaultHistoryField;
        }
        catch (OperationCanceledException) { throw; }
        catch { return DefaultHistoryField; }
    }

    private async Task<(float[]? Vector, HistoryOutcome.Failure? Failure)> EmbedAsync(string id, CancellationToken ct)
    {
        try { return (await embedder.EmbedAsync($"history {id}", ct), null); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetAsync: embedder failed");
            return (null, new HistoryOutcome.Failure(HistoryError.EmbeddingFailed, $"Embedder failed: {ex.Message}"));
        }
    }

    private async Task<(IReadOnlyList<DocumentSearchResult>? Hits, HistoryOutcome.Failure? Failure)> SearchAsync(
        string collection, float[] queryVec, string historyField, string id, CancellationToken ct)
    {
        try
        {
            var opts = new SearchOptions(
                TopK: 50,
                ScoreThreshold: 0,
                HistoryFieldFilter: (historyField, id));
            return (await store.SearchAsync(collection, queryVec, opts, ct), null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetAsync: store search failed for {Collection}", collection);
            return (null, new HistoryOutcome.Failure(
                HistoryError.StoreSearchFailed,
                $"Document store search failed: {ex.Message}",
                new Dictionary<string, object?> { ["collection"] = collection }));
        }
    }
}
