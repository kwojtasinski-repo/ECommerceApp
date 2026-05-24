namespace RagTools.Core;

/// <summary>
/// Abstraction over any embedding model (ONNX, Ollama, …).
/// Registered as a singleton in DI.  All consumers use <see cref="IEmbedder"/> —
/// they are unaware of the concrete implementation or the pre/post-processing pipeline
/// wrapped around it by <see cref="EmbedderPipelineBuilder"/>.
/// </summary>
public interface IEmbedder : IDisposable
{
    /// <summary>Number of dimensions produced by this embedder.</summary>
    int Dimensions { get; }

    /// <summary>
    /// Embed a single text string.  Used by the query path.
    /// Preprocessors are invoked with <see cref="EmbedContext.Query"/> so
    /// they can apply query-specific transforms (e.g. multilingual expansion).
    /// </summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Embed a batch of texts.  Used by the ingest path (hundreds of chunks).
    /// Preprocessors are invoked with <see cref="EmbedContext.Ingest"/> so
    /// they can skip query-only transforms (e.g. glossary expansion is skipped —
    /// document vectors stay pure).
    /// <para>
    /// Default implementation: sequential <see cref="EmbedAsync"/> calls.
    /// Implementations should override this when a native batch API is available
    /// (e.g. <c>OnnxEmbedder</c> overrides to run a single ONNX inference).
    /// </para>
    /// </summary>
    Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        => Task.WhenAll(texts.Select(t => EmbedAsync(t, ct)));
}
