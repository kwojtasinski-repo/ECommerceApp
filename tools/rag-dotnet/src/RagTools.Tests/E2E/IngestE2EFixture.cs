using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RagTools.Core;
using Testcontainers.Qdrant;

namespace RagTools.Tests.E2E;

/// <summary>
/// xUnit class fixture for ingest pipeline E2E tests.
///
/// Setup (once per test class):
///   1. Detect ONNX model directory — skip all tests if absent.
///   2. Start Qdrant in Docker (Testcontainers) or use QDRANT_URL env var.
///   3. Create a self-contained workspace + collection.
///   4. Wire up the full ingest pipeline (IngestChannel, IngestWorker, OperationStore,
///      QdrantDocumentStore, CachedDocumentStore) so tests can push jobs and assert results.
///   5. Expose helpers: EnqueueAndWaitAsync (push job, poll until terminal state).
///
/// Teardown: stop IngestWorker BackgroundService, Qdrant container, temp workspace.
/// </summary>
public sealed class IngestE2EFixture : IAsyncLifetime
{
    public bool IsAvailable { get; private set; }
    public string SkipReason { get; private set; } = string.Empty;

    // Infrastructure
    private QdrantContainer? _container;
    private SyntheticWorkspace? _workspace;
    private IHost? _workerHost;

    // Exposed for tests
    public IngestChannel? Channel { get; private set; }
    public OperationStore? Operations { get; private set; }
    public IDocumentStore? Store { get; private set; }
    public OnnxEmbedder? Embedder { get; private set; }
    public RagConfig? Config { get; private set; }
    public string? Collection { get; private set; }

    private static bool DockerAvailable()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "docker", Arguments = "info",
                RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(5_000);
            return proc?.ExitCode == 0;
        }
        catch { return false; }
    }

    public async Task InitializeAsync()
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"[IngestE2EFixture] InitializeAsync start");

        // Check model availability via shared singleton (avoids double-loading).
        if (!SharedOnnxModel.IsAvailable)
        {
            IsAvailable = false;
            SkipReason  = $"ONNX model not found in {SharedOnnxModel.ModelDir}";  
            Console.WriteLine($"[IngestE2EFixture] SKIP — {SkipReason}");
            return;
        }

        string qdrantUrl;
        var envUrl = Environment.GetEnvironmentVariable("QDRANT_URL");
        if (!string.IsNullOrWhiteSpace(envUrl))
        {
            qdrantUrl = envUrl;
        }
        else if (DockerAvailable())
        {
            Console.WriteLine("[IngestE2EFixture] Starting Qdrant container ...");
            _container = new QdrantBuilder("qdrant/qdrant:v1.13.6")
                .WithPortBinding(6334, assignRandomHostPort: true)
                .Build();
            await _container.StartAsync();
            var grpcPort = _container.GetMappedPublicPort(6334);
            qdrantUrl = $"http://{_container.Hostname}:{grpcPort}";
            Console.WriteLine($"[IngestE2EFixture] Qdrant ready at {qdrantUrl}  (+{sw.Elapsed.TotalSeconds:F1}s)");
        }
        else
        {
            IsAvailable = false;
            SkipReason = "Qdrant not available: set QDRANT_URL or ensure Docker is running.";
            Console.WriteLine($"[IngestE2EFixture] SKIP — {SkipReason}");
            return;
        }

        Collection = $"ingest_e2e_{Guid.NewGuid():N}"[..20];
        _workspace = SyntheticWorkspace.Create(qdrantUrl, Collection, SharedOnnxModel.ModelDir);
        Config = RagConfig.Load(_workspace.ConfigPath);

        // Use the shared singleton — triggers load+warm-up ONCE for the whole test process.
        Console.WriteLine($"[IngestE2EFixture] Acquiring shared ONNX embedder (+{sw.Elapsed.TotalSeconds:F1}s) ...");
        Embedder = SharedOnnxModel.Instance;
        Console.WriteLine($"[IngestE2EFixture] Embedder ready (+{sw.Elapsed.TotalSeconds:F1}s)");

        // Ensure collection exists.
        var rawStore = QdrantStore.Connect(qdrantUrl, Collection);
        await rawStore.EnsureCollectionAsync(Embedder.Dimensions);
        Console.WriteLine($"[IngestE2EFixture] Collection '{Collection}' created (+{sw.Elapsed.TotalSeconds:F1}s)");

        // Build IDocumentStore with caching.
        var qdrantDocStore = new QdrantDocumentStore(qdrantUrl);
        Store = new CachedDocumentStore(qdrantDocStore, new QueryCache());

        // Set up ingest pipeline as a hosted service.
        Channel = new IngestChannel();
        Operations = new OperationStore();

        _workerHost = Host.CreateApplicationBuilder()
            .Build();

        // Wire up IngestWorker manually (DI-free for tests).
        // BertTokenCounter: whitespace-based fallback — no sentencepiece.bpe.model needed for chunking.
        // IngestWorker still uses the shared OnnxEmbedder (fully functional, real vectors).
        var tokenCounter = BertTokenCounter.FromModelDir("/nonexistent/path");
        using var loggerFactory = LoggerFactory.Create(b =>
            b.AddSimpleConsole(o => { o.TimestampFormat = "HH:mm:ss.fff "; })
             .SetMinimumLevel(LogLevel.Debug));
        var worker = new IngestWorker(
            Channel, Store, Embedder, Config, Operations,
            loggerFactory.CreateLogger<IngestWorker>(), tokenCounter);

        // Start the worker as a BackgroundService.
        var cts = new CancellationTokenSource();
        _ = worker.StartAsync(cts.Token);

        IsAvailable = true;
        Console.WriteLine($"[IngestE2EFixture] Ready. Total init: {sw.Elapsed.TotalSeconds:F1}s");
    }

    /// <summary>
    /// Enqueue an ingest job and poll until it reaches Completed or Failed (max 30s).
    /// Returns the final IngestOperationResult.
    /// </summary>
    public async Task<IngestOperationResult?> EnqueueAndWaitAsync(
        string relPath, string content, string? docKind = null,
        int timeoutSeconds = 30)
    {
        var opId = $"{Collection}:{relPath}:{DateTimeOffset.UtcNow.Ticks}";
        var job = new IngestJob
        {
            OperationId = opId,
            Collection  = Collection!,
            RelPath     = relPath,
            Content     = content,
            DocKind     = docKind,
            EnqueuedAt  = DateTimeOffset.UtcNow,
        };

        Operations!.MarkQueued(opId, Collection!, relPath, job.EnqueuedAt);
        Channel!.TryWrite(job);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(200);
            var result = Operations.Get(opId);
            if (result?.Status is IngestStatus.Completed or IngestStatus.Failed)
                return result;
        }

        return Operations.Get(opId);
    }

    public async Task DisposeAsync()
    {
        if (_workerHost is not null)
            await _workerHost.StopAsync(TimeSpan.FromSeconds(5));
        _workspace?.Dispose();
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
