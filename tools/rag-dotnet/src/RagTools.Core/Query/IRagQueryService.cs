namespace RagTools.Core.Query;

/// <summary>
/// Application-level query orchestrator. Encapsulates the full query pipeline
/// (validate → embed → search → weight → bc-filter → postprocess) and returns
/// a typed <see cref="QueryOutcome"/> — no exceptions for expected failure paths.
///
/// MCP, HTTP, and CLI surfaces all delegate to this same service so the pipeline
/// is implemented once.
/// </summary>
public interface IRagQueryService
{
    Task<QueryOutcome> QueryAsync(QueryRequest request, CancellationToken ct = default);
}
