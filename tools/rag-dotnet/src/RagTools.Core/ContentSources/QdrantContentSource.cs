namespace RagTools.Core.ContentSources;

/// <summary>
/// Reads document content from Qdrant full-content points (doc_kind = "full_content").
/// Registered in HTTP mode where the workspace filesystem may not be mounted.
/// Returns <c>null</c> when no content point has been indexed for the given path.
/// </summary>
public sealed class QdrantContentSource(IDocumentStore store) : IContentSource
{
    public async Task<string?> ReadAsync(string collection, string relPath, CancellationToken ct = default)
    {
        var doc = await store.FetchContentAsync(collection, relPath, ct);
        return doc?.Content;
    }
}
