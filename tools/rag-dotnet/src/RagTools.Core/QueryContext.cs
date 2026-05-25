namespace RagTools.Core;

/// <summary>
/// Read-only context passed to <see cref="IResultPostprocessor"/> instances after
/// Qdrant has returned search hits.  Carries enough information for a postprocessor
/// to understand the original request and re-rank or filter accordingly.
/// </summary>
/// <param name="Collection">Qdrant collection that was searched.</param>
/// <param name="OriginalQuestion">The raw user question (before preprocessing).</param>
/// <param name="Topic">Topic / bounded-context substring filter, or null if not specified.</param>
/// <param name="TopK">Maximum number of results the caller wants.</param>
/// <param name="FetchK">Number of results fetched from Qdrant before filtering.</param>
public sealed record QueryContext(
    string Collection,
    string OriginalQuestion,
    string? Topic,
    int TopK,
    int FetchK);
