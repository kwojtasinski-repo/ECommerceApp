using Microsoft.Extensions.Logging;
using RagTools.Core.Shared;

namespace RagTools.Core.Query;

/// <summary>
/// Default <see cref="IRagQueryService"/> — extracted from <c>RagTools.Mcp.Tools.RagTools.QueryDocs</c>
/// so the same pipeline serves MCP, HTTP, and CLI surfaces.
/// </summary>
public sealed class RagQueryService(
    IEmbedder embedder,
    IDocumentStore store,
    RagConfig cfg,
    IEnumerable<IResultPostprocessor> postprocessors,
    ILogger<RagQueryService> logger) : IRagQueryService
{
    public const int MaxTopK = 20;

    public async Task<QueryOutcome> QueryAsync(QueryRequest request, CancellationToken ct = default)
    {
        if (Validate(request) is { } validationFailure)
            return validationFailure;

        var fetchK = request.Bc is not null
            ? Math.Max(cfg.Query.FetchK, request.TopK * 3)
            : cfg.Query.FetchK;

        logger.LogDebug(
            "QueryAsync: collection={Collection} question={Question} bc={Bc} topK={TopK} fetchK={FetchK}",
            request.Collection, request.Question, request.Bc, request.TopK, fetchK);

        var embedResult = await EmbedAsync(request.Question, ct);
        if (embedResult.Failure is not null) return embedResult.Failure;

        var searchResult = await SearchAsync(request.Collection, embedResult.Vector!, fetchK, ct);
        if (searchResult.Failure is not null) return searchResult.Failure;

        var hits = ApplyBcAndTake(searchResult.Hits!, request);

        var postResult = await RunPostprocessorsAsync(hits, request, fetchK, ct);
        if (postResult.Failure is not null) return postResult.Failure;

        logger.LogInformation(
            "QueryAsync: returned {Count}/{Total} hits for '{Question}'",
            postResult.Hits!.Count, searchResult.Hits!.Count, request.Question);

        return new QueryOutcome.Success(BuildResponse(request, postResult.Hits!, searchResult.Hits!.Count));
    }

    private static QueryOutcome.Failure? Validate(QueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return new QueryOutcome.Failure(QueryError.EmptyQuestion, "Question must not be empty or whitespace.");

        if (request.TopK < 1 || request.TopK > MaxTopK)
            return new QueryOutcome.Failure(
                QueryError.TopKOutOfRange,
                $"top_k={request.TopK} is out of range [1, {MaxTopK}].",
                new Dictionary<string, object?> { ["topK"] = request.TopK, ["max"] = MaxTopK });

        return null;
    }

    private async Task<(float[]? Vector, QueryOutcome.Failure? Failure)> EmbedAsync(string question, CancellationToken ct)
    {
        try { return (await embedder.EmbedAsync(question, ct), null); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "QueryAsync: embedder failed");
            return (null, new QueryOutcome.Failure(QueryError.EmbeddingFailed, $"Embedder failed: {ex.Message}"));
        }
    }

    private async Task<(IReadOnlyList<DocumentSearchResult>? Hits, QueryOutcome.Failure? Failure)> SearchAsync(
        string collection, float[] queryVec, int fetchK, CancellationToken ct)
    {
        try
        {
            var opts = new SearchOptions(fetchK, cfg.Query.ScoreThreshold);
            return (await store.SearchAsync(collection, queryVec, opts, ct), null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "QueryAsync: store search failed for {Collection}", collection);
            return (null, new QueryOutcome.Failure(
                QueryError.StoreSearchFailed,
                $"Document store search failed: {ex.Message}",
                new Dictionary<string, object?> { ["collection"] = collection }));
        }
    }

    private IReadOnlyList<DocumentSearchResult> ApplyBcAndTake(IReadOnlyList<DocumentSearchResult> allHits, QueryRequest request)
    {
        var weighted = BcFilter.ApplyWeights(allHits, cfg);
        return request.Bc is not null
            ? weighted.Where(h => BcFilter.Matches(h, request.Bc)).Take(request.TopK).ToList()
            : weighted.Take(request.TopK).ToList();
    }

    private async Task<(IReadOnlyList<DocumentSearchResult>? Hits, QueryOutcome.Failure? Failure)> RunPostprocessorsAsync(
        IReadOnlyList<DocumentSearchResult> hits, QueryRequest request, int fetchK, CancellationToken ct)
    {
        var ctx = new QueryContext(request.Collection, request.Question, request.Bc, request.TopK, fetchK);
        foreach (var pp in postprocessors)
        {
            try { hits = await pp.ProcessAsync(hits, ctx, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "QueryAsync: postprocessor {Pp} failed", pp.GetType().Name);
                return (null, new QueryOutcome.Failure(
                    QueryError.PostprocessorFailed,
                    $"Result postprocessor '{pp.GetType().Name}' failed: {ex.Message}"));
            }
        }
        return (hits, null);
    }

    private static QueryResponse BuildResponse(QueryRequest request, IReadOnlyList<DocumentSearchResult> hits, int totalCandidates) =>
        new(
            Collection: request.Collection,
            Question: request.Question,
            Hits: hits.Select((h, i) => new QueryHit(
                Rank: i + 1,
                Score: Math.Round(h.Score, 3),
                DocKind: h.DocKind,
                RelPath: h.RelPath,
                Breadcrumb: h.Breadcrumb,
                StartLine: h.StartLine,
                Text: h.Text)).ToList(),
            TotalCandidates: totalCandidates);
}
