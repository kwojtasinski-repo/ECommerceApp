using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    // ── Logging sink ──────────────────────────────────────────────────────────
    /// <summary>
    /// Route IngestWorker / IngestController log messages to the currently-running
    /// test's xUnit output.  Each test-class constructor calls
    /// <c>Sink.SetOutput(output)</c>; Dispose calls <c>Sink.SetOutput(null)</c>.
    /// </summary>
    public readonly XunitLogSink Sink = new();

    // ── Availability helpers (shared with IngestE2EFixture) ──────────────────

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
        // Check model availability via shared singleton (avoids double-loading).
        if (!SharedOnnxModel.IsAvailable)
        {
            IsAvailable = false;
            SkipReason  = $"ONNX model not found in {SharedOnnxModel.ModelDir}";
            return;
        }

        var modelDir = SharedOnnxModel.ModelDir;

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

        // Use the shared singleton — triggers load+warm-up ONCE for the whole test process.
        var embedder = SharedOnnxModel.Instance;

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

        // Enable console logging so IngestWorker + controller traces are visible in test output.
        webBuilder.Logging
            .AddProvider(new XunitLoggerProvider(Sink))
            .SetMinimumLevel(LogLevel.Debug);

        webBuilder.WebHost.UseUrls($"http://localhost:{port}");

        webBuilder.Services
            .AddControllers()
            .AddApplicationPart(typeof(RagTools.Mcp.Controllers.IngestController).Assembly)
            .Services
            .AddRouting(opts =>
                opts.ConstraintMap["collection"] = typeof(RagTools.Mcp.Routing.CollectionNameRouteConstraint))
            .AddSingleton(cfg)
            .AddSingleton<IEmbedder>(embedder)
            .AddSingleton<ITokenCounter>(_ => BertTokenCounter.FromModelDir("/nonexistent/path"))
            .AddSingleton<IDocumentStore>(cachedStore)
            .AddSingleton(Operations)
            .AddSingleton<IngestChannel>()
            .AddSingleton<MarkdownChunker>(sp =>
                new MarkdownChunker(cfg.Chunker, sp.GetRequiredService<ITokenCounter>()))
            .AddSingleton<IDocumentProcessor, DocumentProcessor>()
            .AddSingleton<RagTools.Core.Ingest.BatchValidator>()
            .AddSingleton<RagTools.Core.Ingest.IZipBatchParser, RagTools.Core.Ingest.ZipBatchParser>()
            .AddSingleton<IBatchIngestService, BatchIngestService>()
            .AddHostedService<IngestWorker>();

        _app = webBuilder.Build();
        _app.UseMiddleware<ApiKeyMiddleware>();
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
    /// POST a document to the batch ingest endpoint (ZIP with one file) and poll GET until
    /// the operation reaches a terminal state (Completed or Failed). Returns the final status response body.
    /// </summary>
    public async Task<JsonDocument?> UploadAndWaitAsync(
        string collection,
        string relPath,
        string content,
        string? docKind        = null,
        int    timeoutSeconds  = 45,
        string? apiKey         = null)
    {
        // Build a ZIP with required config files plus the document.
        const string MinRagConfigYaml = "embedder:\n  model: BAAI/bge-m3\n";
        const string MinMetaRulesYaml = "doc_kind_rules:\n  - {glob: \"**\", kind: doc}\n";
        const string MinQueriesYaml   = "named_queries:\n  - {name: default, question: test, top_k: 5}\n";
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var ragEntry = zip.CreateEntry("rag-config.yaml");
            using (var w = new StreamWriter(ragEntry.Open(), Encoding.UTF8)) await w.WriteAsync(MinRagConfigYaml);

            var metaEntry = zip.CreateEntry("metadata-rules.yaml");
            using (var w = new StreamWriter(metaEntry.Open(), Encoding.UTF8)) await w.WriteAsync(MinMetaRulesYaml);

            var qEntry = zip.CreateEntry("queries.yaml");
            using (var w = new StreamWriter(qEntry.Open(), Encoding.UTF8)) await w.WriteAsync(MinQueriesYaml);

            var entry = zip.CreateEntry(relPath);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            await writer.WriteAsync(content);
        }
        ms.Position = 0;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/ingest/{collection}/batch")
        {
            Content = new ByteArrayContent(ms.ToArray()),
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        if (apiKey is not null)
            request.Headers.Add("X-Api-Key", apiKey);

        using var postResp = await Client!.SendAsync(request);
        if (!postResp.IsSuccessStatusCode)
            return null;    // caller checks status code separately

        var body       = await postResp.Content.ReadAsStringAsync();
        var batchDoc   = JsonDocument.Parse(body);
        var operations = batchDoc.RootElement.GetProperty("operations").EnumerateArray().ToList();
        if (operations.Count == 0) return null;

        var statusUrl = operations[0].GetProperty("statusUrl").GetString()!;
        var deadline  = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(250);
            using var pollReq = new HttpRequestMessage(HttpMethod.Get, statusUrl);
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
