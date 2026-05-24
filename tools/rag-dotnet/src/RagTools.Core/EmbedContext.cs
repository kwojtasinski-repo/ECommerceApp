namespace RagTools.Core;

/// <summary>
/// Describes why an embedding is being requested.
/// Passed through the preprocessor pipeline so each step can decide
/// whether to apply its transformation (e.g. GlossaryExpansionPreprocessor
/// skips expansion on ingest to keep document vectors pure).
/// </summary>
public enum EmbedPurpose
{
    /// <summary>Embedding a user query. Preprocessors may expand / normalise.</summary>
    Query,

    /// <summary>Embedding a document chunk for storage. Preprocessors should stay minimal.</summary>
    Ingest,
}

/// <summary>
/// Immutable context passed to <see cref="IEmbedderPreprocessor"/> and
/// <see cref="IEmbedderPostprocessor"/> so they can adapt their behaviour based
/// on the reason the embedding is requested.
/// </summary>
/// <param name="Purpose">Query or Ingest.</param>
public sealed record EmbedContext(EmbedPurpose Purpose)
{
    /// <summary>Singleton context for the query path (created once, reused).</summary>
    public static readonly EmbedContext Query = new(EmbedPurpose.Query);

    /// <summary>Singleton context for the ingest path (created once, reused).</summary>
    public static readonly EmbedContext Ingest = new(EmbedPurpose.Ingest);
}
