namespace RagTools.Core.Adrs;

public sealed record AdrListRequest(string Collection);

public enum AdrListError
{
    StoreFetchFailed,
}

public abstract record AdrListOutcome
{
    private AdrListOutcome() { }

    public sealed record Success(AdrListResponse Response) : AdrListOutcome;

    public sealed record Failure(
        AdrListError Error,
        string Message,
        IReadOnlyDictionary<string, object?>? Details = null) : AdrListOutcome;
}

public sealed record AdrListResponse(IReadOnlyList<AdrSummary> Adrs);
