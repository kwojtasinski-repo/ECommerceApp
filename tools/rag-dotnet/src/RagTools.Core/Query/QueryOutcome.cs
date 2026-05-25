namespace RagTools.Core.Query;

/// <summary>
/// Result of a RAG query. Either <see cref="Success"/> with the hits payload,
/// or <see cref="Failure"/> with a typed <see cref="QueryError"/> code and human message.
/// Mirrors the <see cref="RagTools.Core.Ingest.BatchIngestOutcome"/> pattern —
/// no exceptions for expected failure paths.
/// </summary>
public abstract record QueryOutcome
{
    private QueryOutcome() { }

    public sealed record Success(QueryResponse Response) : QueryOutcome;

    public sealed record Failure(
        QueryError Error,
        string Message,
        IReadOnlyDictionary<string, object?>? Details = null) : QueryOutcome;
}

/// <summary>Response body for a successful query.</summary>
public sealed record QueryResponse(
    string Collection,
    string Question,
    IReadOnlyList<QueryHit> Hits,
    int TotalCandidates);

/// <summary>One ranked hit returned by a query.</summary>
public sealed record QueryHit(
    int Rank,
    double Score,
    string DocKind,
    string RelPath,
    string Breadcrumb,
    string Text);
