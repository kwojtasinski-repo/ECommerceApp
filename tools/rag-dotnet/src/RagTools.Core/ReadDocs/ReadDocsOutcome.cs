namespace RagTools.Core.ReadDocs;

public abstract record ReadDocsOutcome
{
    private ReadDocsOutcome() { }

    public sealed record Success(ReadDocsResponse Response) : ReadDocsOutcome;

    public sealed record Failure(
        ReadDocsError Error,
        string Message,
        IReadOnlyDictionary<string, object?>? Details = null) : ReadDocsOutcome;
}

public sealed record ReadDocsResponse(
    string Collection,
    string Question,
    ReadDocsMode Mode,
    IReadOnlyList<ReadDocsFile> Files);

public enum ReadDocsMode { Chunks, Full }

public sealed record ReadDocsFile(
    string RelPath,
    double Score,
    string DocKind,
    ReadDocsMode Mode,
    string? Content,
    IReadOnlyList<ReadDocsChunk> Chunks);

public sealed record ReadDocsChunk(
    int Rank,
    double Score,
    int StartLine,
    string Text);
