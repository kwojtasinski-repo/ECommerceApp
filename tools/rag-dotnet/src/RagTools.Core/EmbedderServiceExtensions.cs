using Microsoft.Extensions.DependencyInjection;

namespace RagTools.Core;

/// <summary>
/// Extension methods on <see cref="IServiceCollection"/> that create an
/// <see cref="EmbedderPipelineBuilder"/> pre-configured for ONNX or Ollama.
///
/// Both methods bake in the default preprocessor stack:
///   1. <see cref="GlossaryExpansionPreprocessor"/> — multilingual query expansion (query-only).
///   2. <see cref="LengthTruncationPreprocessor"/>  — hard-truncate to configured max words
///      (used as a token proxy; symmetric with the Python pipeline's word-based truncation).
///
/// Callers may append additional preprocessors or postprocessors before calling
/// <see cref="EmbedderPipelineBuilder.Register"/>.
/// </summary>
public static class EmbedderServiceExtensions
{
    /// <summary>
    /// Creates an ONNX-backed embedding pipeline.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="modelDir">
    /// Absolute path to the directory containing <c>model.onnx</c> and
    /// <c>sentencepiece.bpe.model</c>.  Resolved by <c>Program.cs</c> from
    /// <c>RAG_MODEL_DIR</c> env var or config default.
    /// </param>
    public static EmbedderPipelineBuilder AddOnnxEmbedderPipeline(
        this IServiceCollection services, string modelDir)
        => new EmbedderPipelineBuilder(services)
            .UseFactory(_ => OnnxEmbedder.Load(modelDir))
            .WithPreprocessor<GlossaryExpansionPreprocessor>()
            .WithPreprocessor<LengthTruncationPreprocessor>();

    /// <summary>
    /// Creates an Ollama-backed embedding pipeline.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="config">Ollama connection settings from <c>rag-config.yaml</c>.</param>
    public static EmbedderPipelineBuilder AddOllamaEmbedderPipeline(
        this IServiceCollection services, OllamaEmbedderConfig config)
        => new EmbedderPipelineBuilder(services)
            .UseFactory(_ => new OllamaEmbedder(config))
            .WithPreprocessor<GlossaryExpansionPreprocessor>()
            .WithPreprocessor<LengthTruncationPreprocessor>();
}
