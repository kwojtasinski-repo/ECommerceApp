namespace RagTools.Core.Adrs;

/// <summary>
/// Application-level list_adrs orchestrator. Reads ADR summaries from the Qdrant index
/// via <see cref="IDocumentStore.ListAdrsAsync"/>.
/// </summary>
public interface IRagListService
{
    Task<AdrListOutcome> ListAsync(AdrListRequest request, CancellationToken ct = default);
}
