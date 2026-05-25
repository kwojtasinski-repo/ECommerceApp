namespace RagTools.Core.ContentSources;

/// <summary>
/// Abstraction for reading the full text content of a document.
///
/// Two implementations registered via DI depending on transport:
///   <see cref="DiskContentSource"/>    — STDIO: reads the workspace filesystem.
///   <see cref="QdrantContentSource"/>  — HTTP:  reads from Qdrant full-content points.
///
/// The <c>collection</c> parameter is the Qdrant collection name (from <c>RagSession.Collection</c>).
/// Returns <c>null</c> when content is not available for the given path.
/// </summary>
public interface IContentSource
{
    Task<string?> ReadAsync(string collection, string relPath, CancellationToken ct = default);
}
