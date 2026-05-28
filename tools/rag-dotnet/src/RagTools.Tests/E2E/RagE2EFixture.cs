using RagTools.Core;
using RagTools.Core.ContentSources;
using Testcontainers.Qdrant;
using RagMcpTools = RagTools.Mcp.Tools.RagTools;

namespace RagTools.Tests.E2E;

/// <summary>
/// xUnit class fixture for .NET RAG e2e tests.
///
/// Setup (once per test class via IClassFixture):
///   1. Detect model directory — skip all tests if ONNX model not found.
///   2. Start Qdrant in a Docker container (Testcontainers) or use QDRANT_URL env var.
///      Skip all tests if neither is available.
///   3. Create a self-contained <see cref="SyntheticWorkspace"/> with generic docs
///      (no EcommerceApp-specific content).
///   4. Run the ingest pipeline programmatically:
///      enumerate → chunk → embed → upsert.
///   5. Expose <see cref="Tools"/> (the MCP tool instance) for test assertions.
///
/// Teardown: workspace temp directory + container stopped.
///
/// Tests are repo-independent: the synthetic workspace is not tied to any project's
/// ADR numbering scheme, domain language, or folder structure conventions.
/// </summary>
public sealed class RagE2EFixture : IAsyncLifetime
{
    // ── Skip state — set during InitializeAsync, read by every test ─────────
    public bool IsAvailable { get; private set; }
    public string SkipReason { get; private set; } = string.Empty;

    // ── Infrastructure ─────────────────────────────────────────────────────
    private QdrantContainer? _container;
    private SyntheticWorkspace? _workspace;
    private OnnxEmbedder? _embedder;
    private QdrantStore? _store;

    /// <summary>
    /// Pre-built MCP tool instance. Null only when <see cref="IsAvailable"/> is false.
    /// </summary>
    public RagMcpTools? Tools { get; private set; }

    // ── Model detection ───────────────────────────────────────────────────
    private static string DefaultModelDir =>
        Environment.GetEnvironmentVariable("RAG_MODEL_DIR")
        ?? Path.GetFullPath(Path.Combine(
            // Resolve relative to test output: bin/Debug/net10.0/ (5 up) → rag-dotnet/ → model/
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "model"));

    private static bool ModelExists(string modelDir) =>
        File.Exists(Path.Combine(modelDir, "model.onnx"));

    // ── Docker detection (for Testcontainers fallback) ────────────────────
    private static bool DockerAvailable()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(5_000);
            return proc?.ExitCode == 0;
        }
        catch { return false; }
    }

    // ── IAsyncLifetime ────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        // 1. Model check.
        var modelDir = Path.GetFullPath(DefaultModelDir);
        if (!ModelExists(modelDir))
        {
            IsAvailable = false;
            SkipReason = $"ONNX model not found at {modelDir}. Run: pwsh tools/rag-dotnet/download-model.ps1";
            return;
        }

        // 2. Qdrant: prefer QDRANT_URL env var, fall back to Testcontainers.
        //    When using Testcontainers we expose port 6334 (gRPC) explicitly and pass its
        //    mapped host port to QdrantStore.Connect() — skips the 6333→6334 auto-conversion.
        string qdrantGrpcUrl;
        var envUrl = Environment.GetEnvironmentVariable("QDRANT_URL");
        if (!string.IsNullOrWhiteSpace(envUrl))
        {
            // Caller-supplied URL — honour as-is (e.g. http://localhost:6333 → gRPC 6334 auto-converted).
            qdrantGrpcUrl = envUrl;
        }
        else if (DockerAvailable())
        {
            _container = new QdrantBuilder("qdrant/qdrant:v1.13.6")
                // Expose gRPC port (6334) with a random host assignment so QdrantClient can connect.
                .WithPortBinding(6334, assignRandomHostPort: true)
                .Build();
            await _container.StartAsync();
            // Use the mapped gRPC port directly — QdrantStore.Connect will not convert it
            // because the port is not 6333.
            var grpcPort = _container.GetMappedPublicPort(6334);
            qdrantGrpcUrl = $"http://{_container.Hostname}:{grpcPort}";
        }
        else
        {
            IsAvailable = false;
            SkipReason =
                "Qdrant not available: set QDRANT_URL or ensure Docker is running for Testcontainers.";
            return;
        }

        // 3. Unique collection name to avoid cross-run conflicts.
        var collection = $"rag_e2e_{Guid.NewGuid():N}"[..24];
        _workspace = SyntheticWorkspace.Create(qdrantGrpcUrl, collection, modelDir);

        // 4. Load config and run ingest programmatically.
        var cfg = RagConfig.Load(_workspace.ConfigPath);
        _embedder = OnnxEmbedder.Load(modelDir);
        _store = QdrantStore.Connect(qdrantGrpcUrl, collection);

        await _store.EnsureCollectionAsync(_embedder.Dimensions);
        await IngestWorkspaceAsync(cfg, _embedder, _store);

        // 5. Expose MCP tool instance for direct method calls in tests.
        //    Wrap QdrantStore in QdrantDocumentStore so RagTools uses IDocumentStore.
        var qdrantDocStore = new QdrantDocumentStore(qdrantGrpcUrl);
        var ragSession = new RagSession(new FixedCollectionResolver(cfg.Collection));
        var contentSource = new DiskContentSource(cfg);
        var configSource = new RagTools.Core.Config.FileConfigSource(cfg);
        var queryService = new RagTools.Core.Query.RagQueryService(
            _embedder, qdrantDocStore, configSource,
            Array.Empty<IResultPostprocessor>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RagTools.Core.Query.RagQueryService>.Instance);
        var readDocsService = new RagTools.Core.ReadDocs.RagReadDocsService(
            _embedder, qdrantDocStore, contentSource, configSource,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RagTools.Core.ReadDocs.RagReadDocsService>.Instance);
        var historyService = new RagTools.Core.History.RagHistoryService(
            _embedder, qdrantDocStore,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RagTools.Core.History.RagHistoryService>.Instance);
        var listService = new RagTools.Core.Adrs.RagListService(
            qdrantDocStore,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RagTools.Core.Adrs.RagListService>.Instance);
        Tools = new RagMcpTools(
            queryService, readDocsService, historyService, listService,
            ragSession,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RagMcpTools>.Instance);
        IsAvailable = true;
    }

    public async Task DisposeAsync()
    {
        _store?.Dispose();
        _embedder?.Dispose();
        _workspace?.Dispose();

        if (_container is not null)
            await _container.DisposeAsync();
    }

    // ── Ingest pipeline (mirrors RagTools.Ingest/Program.cs) ─────────────

    private static async Task IngestWorkspaceAsync(
        RagConfig cfg,
        OnnxEmbedder embedder,
        QdrantStore store)
    {
        var workspaceRoot = cfg.Workspace;
        var tokenCounter = BertTokenCounter.FromModelDir(
            Environment.GetEnvironmentVariable("RAG_MODEL_DIR")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "model")));
        var chunker = new MarkdownChunker(cfg.Chunker, tokenCounter);
        const int batchSize = 32;

        foreach (var sourceRoot in cfg.Source.Roots)
        {
            var absRoot = Path.Combine(workspaceRoot, sourceRoot);
            if (!Directory.Exists(absRoot)) continue;

            foreach (var file in Directory.EnumerateFiles(absRoot, "*.md", SearchOption.AllDirectories))
            {
                var relPath = Path.GetRelativePath(workspaceRoot, file).Replace('\\', '/');
                var text = await File.ReadAllTextAsync(file);
                var chunks = chunker.Chunk(text, relPath);
                if (chunks.Count == 0) continue;

                var docKind = cfg.DetectDocKind(relPath);
                var adrId = cfg.DetectAdrId(relPath);
                var weight = ResolveWeight(relPath, cfg.Ranking);
                var docTitle = ExtractTitle(text, relPath);

                var points = new List<RagPoint>();
                for (var i = 0; i < chunks.Count; i += batchSize)
                {
                    var batch = chunks.Skip(i).Take(batchSize).ToList();
                    // Embed breadcrumb + text (mirrors Program.cs ingest).
                    var texts = batch.Select(c => c.Breadcrumb + "\n\n" + c.Text).ToList();
                    var vectors = embedder.EmbedBatch(texts);

                    for (var j = 0; j < batch.Count; j++)
                    {
                        var chunk = batch[j];
                        points.Add(new RagPoint(
                            Id: ManifestService.StableId(relPath, chunk.Breadcrumb, chunk.StartLine),
                            Vector: vectors[j],
                            Payload: new RagPayload(
                                RelPath: relPath,
                                DocTitle: docTitle,
                                DocKind: docKind,
                                AdrId: adrId,
                                Breadcrumb: chunk.Breadcrumb,
                                HeadingPath: chunk.HeadingPath,
                                StartLine: chunk.StartLine,
                                EndLine: chunk.EndLine,
                                TokenCount: chunk.TokenCount,
                                Weight: weight,
                                Text: chunk.Text)));
                    }
                }

                await store.UpsertAsync(points);
            }
        }
    }

    private static string ExtractTitle(string text, string relPath)
    {
        foreach (var line in text.Split('\n'))
        {
            var s = line.Trim();
            if (s.StartsWith("# ")) return s[2..].Trim();
            if (!string.IsNullOrEmpty(s) && !s.StartsWith('#') && !s.StartsWith("---"))
                break;
        }
        return relPath;
    }

    private static float ResolveWeight(string relPath, RankingSection ranking)
    {
        var p = relPath.Replace('\\', '/');
        foreach (var entry in ranking.Weights)
            if (GlobMatch(p, entry.Pattern))
                return entry.Weight;
        return 1.0f;
    }

    private static bool GlobMatch(string path, string glob)
    {
        var pattern = "^" +
            System.Text.RegularExpressions.Regex.Escape(glob)
                .Replace(@"\*\*", "\u00a7\u00a7")
                .Replace(@"\*", "[^/]*")
                .Replace(@"\?", "[^/]")
                .Replace("\u00a7\u00a7", ".*")
            + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(path, pattern);
    }
}
