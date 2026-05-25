namespace RagTools.Core.Query;

/// <summary>
/// Input to <see cref="IRagQueryService.QueryAsync"/>.
/// Collection comes from the caller (resolved upstream — MCP via <c>RagSession.Collection</c>,
/// HTTP via route value) so the service has no dependency on session state.
/// </summary>
public sealed record QueryRequest(
    string Collection,
    string Question,
    string? Topic = null,
    int TopK = 5);
