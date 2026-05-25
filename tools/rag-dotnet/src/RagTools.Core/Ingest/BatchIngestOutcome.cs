namespace RagTools.Core.Ingest;

/// <summary>
/// Result of a batch ingest submission. Either <see cref="Success"/> with the queued response,
/// or <see cref="Failure"/> with a typed <see cref="BatchIngestError"/> code and human message.
/// No exceptions are thrown for expected failure paths — they are returned as values.
/// </summary>
public abstract record BatchIngestOutcome
{
    private BatchIngestOutcome() { }

    public sealed record Success(BatchIngestResponse Response) : BatchIngestOutcome;

    public sealed record Failure(
        BatchIngestError Error,
        string Message,
        IReadOnlyDictionary<string, object?>? Details = null) : BatchIngestOutcome;
}

/// <summary>Response body for a successfully accepted batch ingest.</summary>
public sealed record BatchIngestResponse(
    string BatchId,
    int Count,
    IReadOnlyList<BatchOperationEntry> Operations,
    IReadOnlyList<string>? Warnings = null);

/// <summary>Per-file entry within <see cref="BatchIngestResponse"/>.</summary>
public sealed record BatchOperationEntry(string OperationId, string RelPath, string StatusUrl);
