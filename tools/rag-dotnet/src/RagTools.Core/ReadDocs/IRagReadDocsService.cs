namespace RagTools.Core.ReadDocs;

/// <summary>
/// Application-level read_docs orchestrator. Encapsulates embed → search → weight →
/// bc-filter → group-by-file → (optional) full-content fetch.
/// </summary>
public interface IRagReadDocsService
{
    Task<ReadDocsOutcome> ReadAsync(ReadDocsRequest request, CancellationToken ct = default);
}
