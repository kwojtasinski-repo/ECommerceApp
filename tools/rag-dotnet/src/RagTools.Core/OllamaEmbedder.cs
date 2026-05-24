using System.Net.Http.Json;

namespace RagTools.Core;

/// <summary>
/// Connection settings for the Ollama embedding provider.
/// Pass directly to <see cref="OllamaEmbedder"/> or <c>AddOllamaEmbedderPipeline</c>.
/// </summary>
public sealed record OllamaEmbedderConfig
{
    public string ApiUrl { get; init; } = "http://localhost:11434";
    public string Model { get; init; } = "nomic-embed-text";
    public int TimeoutSeconds { get; init; } = 30;
}

/// <summary>
/// <see cref="IEmbedder"/> implementation that calls the Ollama REST API
/// (<c>POST /api/embed</c>) to generate embeddings.
///
/// Requires Ollama ≥ 0.3.x (array <c>input</c> field supported).
///
/// <c>EmbedBatchAsync</c> inherits the default sequential implementation from
/// <see cref="IEmbedder"/> — each text is sent in a separate HTTP call.
/// A native batch override can be added later once Ollama version assumptions
/// are validated in the deployment environment.
/// </summary>
public sealed class OllamaEmbedder : IEmbedder
{
    private readonly HttpClient _http;
    private readonly string _model;
    private int _dimensions = -1;

    public OllamaEmbedder(OllamaEmbedderConfig config)
    {
        _model = config.Model;
        _http = new HttpClient
        {
            BaseAddress = new Uri(config.ApiUrl),
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds),
        };
    }

    /// <summary>
    /// Resolved lazily from the first successful embed call.
    /// Throws <see cref="InvalidOperationException"/> if called before any embed.
    /// </summary>
    public int Dimensions => _dimensions > 0
        ? _dimensions
        : throw new InvalidOperationException(
            "OllamaEmbedder.Dimensions is available only after the first EmbedAsync call.");

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var payload = new OllamaEmbedRequest(_model, text);
        using var response = await _http.PostAsJsonAsync("/api/embed", payload, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(ct)
            ?? throw new InvalidOperationException("Ollama /api/embed returned null response.");

        if (result.Embeddings is not { Length: > 0 })
        {
            throw new InvalidOperationException(
                $"Ollama /api/embed returned no embeddings for model '{_model}'.");
        }

        var vec = result.Embeddings[0];
        _dimensions = vec.Length;
        return vec;
    }

    // Inherits default EmbedBatchAsync from IEmbedder (sequential EmbedAsync calls).

    public void Dispose() => _http.Dispose();

    // ── Private request / response DTOs ──────────────────────────────────────

    private sealed record OllamaEmbedRequest(string Model, string Input);

    private sealed record OllamaEmbedResponse(float[][] Embeddings);
}
