namespace RagTools.Core.History;

public sealed record HistoryRequest(string Collection, string Id);

public enum HistoryError
{
    EmptyId,
    EmbeddingFailed,
    StoreSearchFailed,
}

public abstract record HistoryOutcome
{
    private HistoryOutcome() { }

    public sealed record Success(HistoryResponse Response) : HistoryOutcome;

    public sealed record Failure(
        HistoryError Error,
        string Message,
        IReadOnlyDictionary<string, object?>? Details = null) : HistoryOutcome;
}

public sealed record HistoryResponse(
    string Id,
    string HistoryField,
    IReadOnlyList<HistoryChunk> Chunks);

public sealed record HistoryChunk(
    string RelPath,
    string Breadcrumb,
    string DocKind,
    int StartLine,
    string Text);
