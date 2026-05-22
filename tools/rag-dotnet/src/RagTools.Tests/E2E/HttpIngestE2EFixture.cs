using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RagTools.Core;
using RagTools.Mcp.Controllers;
using RagTools.Mcp.Middleware;
using Testcontainers.Qdrant;

namespace RagTools.Tests.E2E;

/// <summary>
/// xUnit class fixture that boots the full ASP.NET Core ingest server in-process
/// (WebApplication with Kestrel on a free port) and exposes an <see cref="HttpClient"/>
/// for black-box HTTP testing.
///
/// The server wiring mirrors the SSE branch of Program.cs exactly:
///   ApiKeyMiddleware → IngestController → IngestChannel → IngestWorker → Qdrant
///
/// RAG_API_KEY is deliberately left unset so the middleware runs in dev-mode (allow all).
/// Tests that want to exercise auth should set the header explicitly and configure the key
/// via HttpIngestE2EFixture.TestApiKey.
///
/// Skip condition: ONNX model absent OR Qdrant unavailable (same as IngestE2EFixture).
/// </summary>
public sealed class HttpIngestE2EFixture : IAsyncLifetime
{
    public bool   IsAvailable { get; private set; }
    public string SkipReason  { get; private set; } = string.Empty;

    // Exposed to tests
    public HttpClient?      Client     { get; private set; }
    public IDocumentStore?  Store      { get; private set; }
    public OperationStore?  Operations { get; private set; }
    public string?          Collection { get; private set; }
    public string?          BaseUrl    { get; private set; }

    // Set this before using Client if you want to pre-populate the header.
    public string? TestApiKey { get; set; }

    private QdrantContainer?  _container;
    private WebApplication?   _app;
    private SyntheticWorkspace? _workspace;
    private QdrantStore?      _rawStore;
    private string?           _qdrantUrl;

    // ── Availability helpers (shared with IngestE2EFixture) ──────────────────

    private static string DefaultModelDir =>
        Environment.GetEnvironmentVariable("RAG_MODEL_DIR")
        ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "model"));

    private static bool ModelExists(string dir) =>
        File.Exists(Path.Combine(dir, "model.onnx"));

    private static bool DockerAvailable()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName               = "docker",
                Arguments              = "info",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(5_000);
            return proc?.ExitCode == 0;
        }
        catch { return false; }
    }

    /// <summary>Finds a free TCP port on localhost (small race window — acceptable for tests).</summary>
    private static int FindFreePort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }

    // ── IAsyncLifetime ────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        var modelDir = DefaultModelDir;
        if (!ModelExists(modelDir))
        {
            IsAvailable = false;
            SkipReason  = $"ONNX model not found at {modelDir}. Run: pwsh tools/rag-dotnet/download-model.ps1";
            return;
        }

        // Start Qdrant (Testcontainers or env-provided URL).
        string qdrantUrl;
        var envUrl = Environment.GetEnvironmentVariable("QDRANT_URL");
        if (!string.IsNullOrWhiteSpace(envUrl))
        {
            qdrantUrl = envUrl;
        }
        else if (DockerAvailable())
        {
            _container = new QdrantBuilder("qdrant/qdrant:v1.13.6")
                .WithPortBinding(6334, assignRandomHostPort: true)
                .Build();
            await _container.StartAsync();
            var grpcPort = _container.GetMappedPublicPort(6334);
            qdrantUrl = $"http://{_container.Hostname}:{grpcPort}";
        }
        else
        {
            IsAvailable = false;
            SkipReason  = "Qdrant not available: set QDRANT_URL or ensure Docker is running.";
            return;
        }

        Collection = $"http_e2e_{Guid.NewGuid():N}"[..18];
        _workspace = SyntheticWorkspace.Create(qdrantUrl, Collection, modelDir);
        var cfg = RagConfig.Load(_workspace.ConfigPath);

        // Ensure Qdrant collection exists.
        var embedder = OnnxEmbedder.Load(modelDir);
        _qdrantUrl = qdrantUrl;
        _rawStore  = QdrantStore.Connect(qdrantUrl, Collection);
        await _rawStore.EnsureCollectionAsync(embedder.Dimensions);

        // Build the IDocumentStore (same as SSE branch of Program.cs).
        var qdrantDocStore = new QdrantDocumentStore(_qdrantUrl!);
        var cachedStore    = new CachedDocumentStore(qdrantDocStore, new QueryCache());
        Store      = cachedStore;
        Operations = new OperationStore();

        // ── Build WebApplication (mirrors SSE branch of Program.cs) ──────────
        var port       = FindFreePort();
        var webBuilder = WebApplication.CreateBuilder();

        // Suppress console noise from the test server.
        webBuilder.Logging.ClearProviders();
        webBuilder.Logging.AddProvider(NullLoggerProvider.Instance);

        webBuilder.WebHost.UseUrls($"http://localhost:{port}");

        webBuilder.Services
            .AddControllers()
            .Services
            .AddSingleton(cfg)
            .AddSingleton(embedder)
            .AddSingleton<IDocumentStore>(cachedStore)
            .AddSingleton(Operations)
            .AddSingleton<IngestChannel>()
            .AddHostedService<IngestWorker>()
            .AddScoped<RagSession>();

        _app = webBuilder.Build();
        _app.UseMiddleware<ApiKeyMiddleware>();
        _app.UseMiddleware<RagSessionMiddleware>();
        _app.MapControllers();

        await _app.StartAsync();

        BaseUrl = $"http://localhost:{port}";
        Client  = new HttpClient { BaseAddress = new Uri(BaseUrl) };

        IsAvailable = true;
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_app is not null)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _app.StopAsync(cts.Token);
            await _app.DisposeAsync();
        }
        // Clean up: delete the test collection from Qdrant so orphaned collections
        // do not accumulate across test runs. Best-effort — swallow any errors.
        if (_rawStore is not null && Collection is not null)
        {
            try
            {
                await _rawStore.TryDeleteCollectionAsync(Collection);
            }
            catch { /* best-effort cleanup */ }
            _rawStore.Dispose();
        }
        _workspace?.Dispose();
        if (_container is not null)
            await _container.DisposeAsync();
    }

    // ── Test helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// POST a document to the ingest endpoint and poll GET until the operation
    /// reaches a terminal state (Completed or Failed). Returns the final status response body.
    /// </summary>
    public async Task<JsonDocument?> UploadAndWaitAsync(
        string collection,
        string relPath,
        string content,
        string? docKind        = null,
        int    timeoutSeconds  = 45,
        string? apiKey         = null)
    {
        var payload = JsonSerializer.Serialize(new
        {
            relPath,
            content,
            docKind,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/ingest/{collection}")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
        };
        if (apiKey is not null)
            request.Headers.Add("X-Api-Key", apiKey);

        using var postResp = await Client!.SendAsync(request);
        if (!postResp.IsSuccessStatusCode)
            return null;    // caller checks status code separately

        var body    = await postResp.Content.ReadAsStringAsync();
        var doc     = JsonDocument.Parse(body);
        var opId    = doc.RootElement.GetProperty("operationId").GetString()!;
        var pollUrl = $"/ingest/{collection}/operations/{Uri.EscapeDataString(opId)}";

        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(250);
            using var pollReq = new HttpRequestMessage(HttpMethod.Get, pollUrl);
            if (apiKey is not null)
                pollReq.Headers.Add("X-Api-Key", apiKey);

            using var pollResp = await Client.SendAsync(pollReq);
            if (!pollResp.IsSuccessStatusCode)
                continue;

            var pollBody   = await pollResp.Content.ReadAsStringAsync();
            var pollDoc    = JsonDocument.Parse(pollBody);
            var status     = pollDoc.RootElement.GetProperty("status").GetString();
            if (status is "Completed" or "Failed")
                return JsonDocument.Parse(pollBody);   // re-parse to return a standalone document
        }

        return null;    // timed out
    }
}
