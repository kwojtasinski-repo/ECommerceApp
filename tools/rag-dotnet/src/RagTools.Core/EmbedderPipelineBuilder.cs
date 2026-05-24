using Microsoft.Extensions.DependencyInjection;

namespace RagTools.Core;

/// <summary>
/// Fluent builder that assembles a <see cref="PipelinedEmbedder"/> and registers it
/// as <see cref="IEmbedder"/> in the DI container.
///
/// Entry points (called from <c>Program.cs</c>):
/// <list type="bullet">
///   <item><see cref="EmbedderServiceExtensions.AddOnnxEmbedderPipeline"/></item>
///   <item><see cref="EmbedderServiceExtensions.AddOllamaEmbedderPipeline"/></item>
/// </list>
///
/// Usage:
/// <code>
/// services.AddOnnxEmbedderPipeline(modelDir)
///         .WithPreprocessor&lt;MyExtraPreprocessor&gt;()
///         .Register();
/// </code>
/// </summary>
public sealed class EmbedderPipelineBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<Type> _preprocessorTypes = [];
    private readonly List<Type> _postprocessorTypes = [];
    private Func<IServiceProvider, IEmbedder>? _innerFactory;

    internal EmbedderPipelineBuilder(IServiceCollection services)
        => _services = services;

    // ── Fluent API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Register a preprocessor to run BEFORE embedding.
    /// Preprocessors are called in the order they are added.
    /// </summary>
    public EmbedderPipelineBuilder WithPreprocessor<T>()
        where T : class, IEmbedderPreprocessor
    {
        _preprocessorTypes.Add(typeof(T));
        return this;
    }

    /// <summary>
    /// Register a postprocessor to run AFTER embedding.
    /// Postprocessors are called in the order they are added.
    /// </summary>
    public EmbedderPipelineBuilder WithPostprocessor<T>()
        where T : class, IEmbedderPostprocessor
    {
        _postprocessorTypes.Add(typeof(T));
        return this;
    }

    /// <summary>Sets the factory that creates the concrete inner embedder.</summary>
    internal EmbedderPipelineBuilder UseFactory(Func<IServiceProvider, IEmbedder> factory)
    {
        _innerFactory = factory;
        return this;
    }

    // ── Terminal ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers all preprocessors, postprocessors, and the <see cref="PipelinedEmbedder"/>
    /// as <see cref="IEmbedder"/> singleton.  Must be called once to complete setup.
    /// </summary>
    public IServiceCollection Register()
    {
        if (_innerFactory is null)
        {
            throw new InvalidOperationException(
                "No embedder factory set.  Use AddOnnxEmbedderPipeline or AddOllamaEmbedderPipeline.");
        }

        // 1. Preprocessors — each type registered as IEmbedderPreprocessor singleton.
        //    GetServices<IEmbedderPreprocessor>() returns them in registration order.
        foreach (var t in _preprocessorTypes)
        {
            _services.AddSingleton(typeof(IEmbedderPreprocessor), t);
        }

        // 2. Postprocessors — same pattern.
        foreach (var t in _postprocessorTypes)
        {
            _services.AddSingleton(typeof(IEmbedderPostprocessor), t);
        }

        // 3. Inner embedder registered under a private marker type so it does not
        //    conflict with the IEmbedder registration we add in step 4.
        _services.AddSingleton<InnerEmbedderMarker>(sp =>
            new InnerEmbedderMarker(_innerFactory(sp)));

        // 4. IEmbedder = PipelinedEmbedder wrapping the inner + ordered pipelines.
        _services.AddSingleton<IEmbedder>(sp => new PipelinedEmbedder(
            sp.GetRequiredService<InnerEmbedderMarker>().Value,
            sp.GetServices<IEmbedderPreprocessor>().ToList().AsReadOnly(),
            sp.GetServices<IEmbedderPostprocessor>().ToList().AsReadOnly()));

        return _services;
    }

    // Thin wrapper — keeps the inner IEmbedder out of the IEmbedder slot until
    // PipelinedEmbedder is ready to take that slot.
    private sealed record InnerEmbedderMarker(IEmbedder Value);
}
