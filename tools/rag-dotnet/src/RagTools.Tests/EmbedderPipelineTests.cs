using Microsoft.Extensions.DependencyInjection;
using RagTools.Core;
using Xunit;

namespace RagTools.Tests;

/// <summary>
/// Unit tests for EmbedderPipelineBuilder, PipelinedEmbedder, and the built-in preprocessors.
/// Uses a Fake inner embedder — no ONNX model or Ollama required.
/// </summary>
public class EmbedderPipelineTests
{
    // ── Fakes ────────────────────────────────────────────────────────────────

    /// <summary>Records every call for assertion.</summary>
    private sealed class FakeEmbedder : IEmbedder
    {
        public int Dimensions => 4;
        public List<(string Text, string Purpose)> Calls { get; } = [];

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            Calls.Add((text, "single"));
            return Task.FromResult(new float[] { 1f, 2f, 3f, 4f });
        }

        public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        {
            foreach (var t in texts)
                Calls.Add((t, "batch"));
            return Task.FromResult(texts.Select(_ => new float[] { 1f, 2f, 3f, 4f }).ToArray());
        }

        public void Dispose() { }
    }

    private sealed class AppendPreprocessor(string suffix) : IEmbedderPreprocessor
    {
        public List<(string Text, EmbedPurpose Purpose)> Calls { get; } = [];

        public Task<string> ProcessAsync(string text, EmbedContext ctx, CancellationToken ct = default)
        {
            Calls.Add((text, ctx.Purpose));
            return Task.FromResult(text + suffix);
        }
    }

    private sealed class DoublePostprocessor : IEmbedderPostprocessor
    {
        public int CallCount { get; private set; }

        public Task<float[]> ProcessAsync(float[] vector, EmbedContext ctx, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(vector.Select(v => v * 2f).ToArray());
        }
    }

    // ── Builder smoke test ───────────────────────────────────────────────────

    [Fact]
    public async Task Register_WiresIEmbedderAsPipelinedEmbedder()
    {
        var inner = new FakeEmbedder();
        var services = new ServiceCollection();
        new EmbedderPipelineBuilder(services)
            .UseFactory(_ => inner)
            .Register();

        var provider = services.BuildServiceProvider();
        var embedder = provider.GetRequiredService<IEmbedder>();

        // Should be PipelinedEmbedder, not FakeEmbedder directly.
        Assert.IsNotType<FakeEmbedder>(embedder);

        var result = await embedder.EmbedAsync("hello");
        Assert.Equal(4, result.Length);
        Assert.Single(inner.Calls);
    }

    // ── Preprocessor ordering ────────────────────────────────────────────────

    [Fact]
    public async Task EmbedAsync_RunsPreprocessorsInOrder()
    {
        var inner = new FakeEmbedder();
        var services = new ServiceCollection();
        new EmbedderPipelineBuilder(services)
            .UseFactory(_ => inner)
            .WithPreprocessor<AppendAPreprocessor>()
            .WithPreprocessor<AppendBPreprocessor>()
            .Register();

        var provider = services.BuildServiceProvider();
        var embedder = provider.GetRequiredService<IEmbedder>();

        await embedder.EmbedAsync("x");

        // Inner should have received "x_A_B" — preprocessors applied in order.
        Assert.Equal("x_A_B", inner.Calls[0].Text);
    }

    // ── EmbedContext: Query vs Ingest ─────────────────────────────────────────

    [Fact]
    public async Task EmbedAsync_SendsQueryContext()
    {
        var pre = new AppendPreprocessor("_q");
        var inner = new FakeEmbedder();
        var services = new ServiceCollection()
            .AddSingleton<IEmbedderPreprocessor>(pre);
        new EmbedderPipelineBuilder(services)
            .UseFactory(_ => inner)
            .Register();
        var embedder = services.BuildServiceProvider().GetRequiredService<IEmbedder>();

        await embedder.EmbedAsync("hello");

        Assert.Equal(EmbedPurpose.Query, pre.Calls[0].Purpose);
    }

    [Fact]
    public async Task EmbedBatchAsync_SendsIngestContext()
    {
        var pre = new AppendPreprocessor("_i");
        var inner = new FakeEmbedder();
        var services = new ServiceCollection()
            .AddSingleton<IEmbedderPreprocessor>(pre);
        new EmbedderPipelineBuilder(services)
            .UseFactory(_ => inner)
            .Register();
        var embedder = services.BuildServiceProvider().GetRequiredService<IEmbedder>();

        await embedder.EmbedBatchAsync(["a", "b"]);

        Assert.All(pre.Calls, c => Assert.Equal(EmbedPurpose.Ingest, c.Purpose));
    }

    // ── Postprocessor ────────────────────────────────────────────────────────

    [Fact]
    public async Task EmbedAsync_RunsPostprocessor()
    {
        var post = new DoublePostprocessor();
        var inner = new FakeEmbedder();           // returns [1,2,3,4]
        var services = new ServiceCollection()
            .AddSingleton<IEmbedderPostprocessor>(post);
        new EmbedderPipelineBuilder(services)
            .UseFactory(_ => inner)
            .Register();
        var embedder = services.BuildServiceProvider().GetRequiredService<IEmbedder>();

        var result = await embedder.EmbedAsync("hello");

        Assert.Equal([2f, 4f, 6f, 8f], result);
        Assert.Equal(1, post.CallCount);
    }

    // ── No preprocessors / postprocessors ────────────────────────────────────

    [Fact]
    public async Task Pipeline_NoProcessors_PassesThrough()
    {
        var inner = new FakeEmbedder();
        var services = new ServiceCollection();
        new EmbedderPipelineBuilder(services)
            .UseFactory(_ => inner)
            .Register();
        var embedder = services.BuildServiceProvider().GetRequiredService<IEmbedder>();

        var result = await embedder.EmbedAsync("raw");

        Assert.Equal("raw", inner.Calls[0].Text);
        Assert.Equal([1f, 2f, 3f, 4f], result);
    }

    // ── EmbedBatchAsync preprocesses each text independently ─────────────────

    [Fact]
    public async Task EmbedBatchAsync_PreprocessesEachTextIndependently()
    {
        var inner = new FakeEmbedder();
        var services = new ServiceCollection();
        new EmbedderPipelineBuilder(services)
            .UseFactory(_ => inner)
            .WithPreprocessor<AppendAPreprocessor>()
            .Register();
        var embedder = services.BuildServiceProvider().GetRequiredService<IEmbedder>();

        await embedder.EmbedBatchAsync(["x", "y", "z"]);

        Assert.Equal(["x_A", "y_A", "z_A"], inner.Calls.Select(c => c.Text));
    }

    // ── Builder requires factory ──────────────────────────────────────────────

    [Fact]
    public void Register_WithoutFactory_Throws()
    {
        var services = new ServiceCollection();
        var builder = new EmbedderPipelineBuilder(services);
        Assert.Throws<InvalidOperationException>(() => builder.Register());
    }

    // ── Fake preprocessors for ordering test ─────────────────────────────────

    private sealed class AppendAPreprocessor : IEmbedderPreprocessor
    {
        public Task<string> ProcessAsync(string text, EmbedContext ctx, CancellationToken ct = default)
            => Task.FromResult(text + "_A");
    }

    private sealed class AppendBPreprocessor : IEmbedderPreprocessor
    {
        public Task<string> ProcessAsync(string text, EmbedContext ctx, CancellationToken ct = default)
            => Task.FromResult(text + "_B");
    }
}
