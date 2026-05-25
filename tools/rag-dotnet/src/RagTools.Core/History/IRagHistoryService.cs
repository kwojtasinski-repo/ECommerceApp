namespace RagTools.Core.History;

/// <summary>
/// Application-level get_history orchestrator. Resolves history_field from the collection
/// __config__ point, runs a filtered Qdrant search, and returns chunks in chronological
/// (start-line) order.
/// </summary>
public interface IRagHistoryService
{
    Task<HistoryOutcome> GetAsync(HistoryRequest request, CancellationToken ct = default);
}
