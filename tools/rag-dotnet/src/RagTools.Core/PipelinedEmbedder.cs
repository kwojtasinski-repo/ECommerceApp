namespace RagTools.Core;

/// <summary>
/// Composite <see cref="IEmbedder"/> that wraps a concrete implementation (ONNX or Ollama)
/// and runs ordered pre- and post-processing pipelines around it.
///
/// Registered as <see cref="IEmbedder"/> singleton by <see cref="EmbedderPipelineBuilder"/>.
/// All consumers receive this type transparently — they never reference it directly.
/// </summary>
internal sealed class PipelinedEmbedder(
    IEmbedder inner,
    IReadOnlyList<IEmbedderPreprocessor> preprocessors,
    IReadOnlyList<IEmbedderPostprocessor> postprocessors) : IEmbedder
{
    public int Dimensions => inner.Dimensions;

    /// <summary>
    /// Query path: preprocessors run with <see cref="EmbedContext.Query"/>
    /// (allows glossary expansion and other query-specific transforms).
    /// </summary>
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var ctx = EmbedContext.Query;

        var processed = text;
        foreach (var pre in preprocessors)
            processed = await pre.ProcessAsync(processed, ctx, ct);

        var vector = await inner.EmbedAsync(processed, ct);

        foreach (var post in postprocessors)
            vector = await post.ProcessAsync(vector, ctx, ct);

        return vector;
    }

    /// <summary>
    /// Ingest path: preprocessors run with <see cref="EmbedContext.Ingest"/>
    /// (glossary expansion is skipped — document vectors stay pure).
    /// </summary>
    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var ctx = EmbedContext.Ingest;

        // ① Preprocess each text independently.
        var processed = new string[texts.Count];
        for (var i = 0; i < texts.Count; i++)
        {
            var t = texts[i];
            foreach (var pre in preprocessors)
                t = await pre.ProcessAsync(t, ctx, ct);
            processed[i] = t;
        }

        // ② Delegate to inner — OnnxEmbedder uses its native batch path here.
        var vectors = await inner.EmbedBatchAsync(processed, ct);

        // ③ Postprocess each vector.
        for (var i = 0; i < vectors.Length; i++)
            foreach (var post in postprocessors)
                vectors[i] = await post.ProcessAsync(vectors[i], ctx, ct);

        return vectors;
    }

    public void Dispose() => inner.Dispose();
}
