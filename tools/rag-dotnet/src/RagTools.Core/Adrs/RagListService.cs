using Microsoft.Extensions.Logging;

namespace RagTools.Core.Adrs;

/// <summary>
/// Default <see cref="IRagListService"/> — extracted from
/// <c>RagTools.Mcp.Tools.RagTools.ListAdrs</c>.
/// </summary>
public sealed class RagListService(
    IDocumentStore store,
    ILogger<RagListService> logger) : IRagListService
{
    public async Task<AdrListOutcome> ListAsync(AdrListRequest request, CancellationToken ct = default)
    {
        IReadOnlyList<AdrSummary> summaries;
        try
        {
            summaries = await store.ListAdrsAsync(request.Collection, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ListAsync: store fetch failed for {Collection}", request.Collection);
            return new AdrListOutcome.Failure(
                AdrListError.StoreFetchFailed,
                $"ADR listing failed: {ex.Message}",
                new Dictionary<string, object?> { ["collection"] = request.Collection });
        }

        logger.LogInformation("ListAsync: returned {Count} ADRs from {Collection}", summaries.Count, request.Collection);
        return new AdrListOutcome.Success(new AdrListResponse(summaries));
    }
}
