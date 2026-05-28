using RagTools.Core;
using RagTools.Core.ContentSources;
using Testcontainers.Qdrant;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using RagMcpTools = RagTools.Mcp.Tools.RagTools;

namespace RagTools.Tests.E2E;

/// <summary>
/// xUnit class fixture for auto-mode chunker E2E tests (Levels B and C).
///
/// Setup (once per test class):
///   1. Detect ONNX model directory — skip all tests if absent.
///   2. Start Qdrant via Testcontainers or use QDRANT_URL env var — skip if neither available.
///   3. Create a self-contained workspace with two synthetic fixture docs:
///      - H4 fixture: has H4 headings that auto mode splits into chunk boundaries.
///      - Short-section fixture: has one section below min_tokens that auto mode merges.
///   4. Ingest both docs using auto-mode chunker config (split_on_headings: "auto", min_tokens: 40).
///   5. Expose: Store (for Level B payload scrolling), Tools (for Level C semantic queries),
///      and the individual chunk lists (for direct assertions without Qdrant queries).
/// </summary>
public sealed class AutoModeE2EFixture : IAsyncLifetime
{
    // ── Skip state ─────────────────────────────────────────────────────────
    public bool IsAvailable { get; private set; }
    public string SkipReason { get; private set; } = string.Empty;

    // ── Infrastructure ─────────────────────────────────────────────────────
    private QdrantContainer? _container;
    private TempAutoModeWorkspace? _workspace;
    private OnnxEmbedder? _embedder;

    // ── Exposed for tests ─────────────────────────────────────────────────
    public QdrantStore? Store { get; private set; }
    public RagMcpTools? Tools { get; private set; }
    public string? Collection { get; private set; }
    private string? _qdrantGrpcUrl;

    // ── Fixture document content (shared with Level A tests) ───────────────

    /// <summary>Markdown with H4 headings that auto mode splits into chunk boundaries.</summary>
    internal const string H4Doc = """
        # Service Design

        This document records key architectural decisions about service design.
        Services should be autonomous, have clear boundaries, and communicate
        via well-defined interfaces. Dependencies must flow inward.

        ## API Design

        APIs should be RESTful and consistent across all bounded contexts.
        Use proper HTTP methods: GET for reads, POST for creates,
        PUT for full updates, PATCH for partial updates, DELETE for removes.
        Always version your API endpoints before publishing them.
        Deprecate old versions with at least six months notice.

        ### Resource Naming

        Resources use plural nouns: /orders, /items, /customers.
        Use kebab-case for multi-word resources: /order-items.
        Nested resources indicate ownership: /orders/{id}/items.
        Avoid deeply nested URLs beyond two levels.

        #### Versioning Strategy

        API versions are embedded in the URL path: /api/v1/orders.
        Never break existing versions — only add new ones.
        Deprecation requires a minimum six-month notice period.
        All breaking changes require a major version bump.
        Supporting old versions costs more than you think.
        Plan your deprecation strategy from day one of the API.

        #### Status Codes

        Use 200 for successful GET and PUT.
        Use 201 for successful POST when a resource is created.
        Use 204 for successful DELETE with no content body.
        Use 400 for client validation errors with a details array.
        Use 404 when the requested resource does not exist.
        Use 409 for conflicts such as duplicate resource creation.
        Use 500 only for unexpected server errors, never for business errors.

        ## Data Access

        All data access goes through repository interfaces defined in the domain layer.
        The domain layer defines repository contracts; infrastructure implements them.
        Never access the database directly from application services.

        ### Repository Pattern

        Repositories abstract the persistence mechanism from business logic.
        They work with domain aggregates, not data transfer objects.
        A repository must never return partially initialised aggregates.

        #### Unit of Work

        Changes are committed atomically via the unit of work pattern.
        No partial saves — either everything commits or nothing does.
        The unit of work scope is typically per HTTP request or per command.
        Nested units of work are not supported; avoid them by design.

        #### Connection Management

        Connection pooling is handled exclusively by the infrastructure layer.
        Each request gets its own unit of work scope through dependency injection.
        Never open a connection in domain or application layer code.
        """;

    /// <summary>Markdown with a deliberately short section that auto mode merges instead of dropping.</summary>
    internal const string ShortSectionsDoc = """
        # Release Notes

        ## Version 2.0

        Major rewrite of the order processing subsystem with improved performance,
        stricter validation, and support for multi-currency transactions.
        The API is not backward compatible with version 1.x.
        Migration guide is available in MIGRATION.md.
        See the upgrade checklist in the appendix before proceeding.
        All data must be backed up before starting the upgrade process.

        ## See Also

        See MIGRATION.md for step-by-step upgrade instructions.

        ## Breaking Changes

        The OrderService interface has changed significantly in this release.
        The CreateOrder method now requires a UserId parameter for audit logging.
        The UpdateOrder method was split into UpdateOrderDetails and UpdateOrderStatus.
        Existing implementations must be updated before upgrading to version 2.0.
        Automated migration scripts are provided for the most common patterns.
        Review the full list of breaking changes in CHANGELOG.md before migrating.

        ## Deprecated APIs

        The following APIs are deprecated and will be removed in version 3.0:

        - LegacyOrderService (use OrderService instead)
        - OldCatalogRepository (use CatalogRepository instead)
        - GetAllOrders without pagination (use the paged overload instead)
        - DirectDatabaseAccess helpers (use repositories instead)

        Plan your migration away from these APIs before the next major release.
        """;

    internal const string H4RelPath    = "docs/auto-mode-h4-sections.md";
    internal const string ShortRelPath = "docs/auto-mode-short-sections.md";
    internal const string H5RelPath    = "docs/auto-mode-h5-sections.md";

    /// <summary>
    /// Document with H5 headings nested under H4.
    /// Used to verify that explicit [1,2,3,4] does NOT split at H5,
    /// while auto mode DOES split at H5 (it detects H5 as the deepest level).
    /// Each H5 section is intentionally &gt;40 words to exceed min_tokens=40.
    /// </summary>
    internal const string H5Doc = """
        # Architecture Guide

        ## Core Concepts

        ### Service Layer

        #### Request Handling

        ##### Synchronous Path
        The synchronous request path is the primary processing route for all incoming API
        calls. Requests are validated against the schema, authenticated via the identity
        provider, and dispatched to the appropriate domain handler. Each handler returns
        a typed result that the API layer maps to an HTTP response code and body.

        ##### Asynchronous Path
        The asynchronous request path queues work items into the internal job broker and
        returns an accepted status immediately to the caller. Background workers pick up
        the queued items, execute the domain logic, and publish completion events. Callers
        poll a status endpoint or subscribe to webhook notifications to learn the outcome.

        #### Response Formatting

        The response formatter applies content negotiation, serialises the domain result
        into the requested media type, attaches correlation headers, and finalises cache
        control directives before returning to the transport layer.

        ## Infrastructure

        Infrastructure services provide persistence, messaging, and external integrations
        used by the service layer described above.
        """;


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

    // ── IAsyncLifetime ────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        var modelDir = Path.GetFullPath(DefaultModelDir);
        if (!ModelExists(modelDir))
        {
            IsAvailable = false;
            SkipReason = $"ONNX model not found at {modelDir}. Run: pwsh tools/rag-dotnet/download-model.ps1";
            return;
        }

        string qdrantGrpcUrl;
        var envUrl = Environment.GetEnvironmentVariable("QDRANT_URL");
        if (!string.IsNullOrWhiteSpace(envUrl))
        {
            qdrantGrpcUrl = envUrl;
        }
        else if (DockerAvailable())
        {
            _container = new QdrantBuilder("qdrant/qdrant:v1.13.6")
                .WithPortBinding(6334, assignRandomHostPort: true)
                .Build();
            await _container.StartAsync();
            var grpcPort = _container.GetMappedPublicPort(6334);
            qdrantGrpcUrl = $"http://{_container.Hostname}:{grpcPort}";
        }
        else
        {
            IsAvailable = false;
            SkipReason = "Qdrant not available: set QDRANT_URL or ensure Docker is running.";
            return;
        }

        Collection = $"auto_mode_e2e_{Guid.NewGuid():N}"[..24];
        _qdrantGrpcUrl = qdrantGrpcUrl;
        _workspace = TempAutoModeWorkspace.Create(qdrantGrpcUrl, Collection, modelDir);

        var cfg = RagConfig.Load(_workspace.ConfigPath);
        _embedder = OnnxEmbedder.Load(modelDir);
        Store     = QdrantStore.Connect(qdrantGrpcUrl, Collection);

        await Store.EnsureCollectionAsync(_embedder.Dimensions);
        await IngestWorkspaceAsync(cfg, _embedder, Store);

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

    /// <summary>
    /// Scroll all Qdrant points for a given relative path and return their breadcrumb + text.
    /// Returns empty list when fixture is unavailable.
    /// </summary>
    public async Task<List<(string Breadcrumb, string Text)>> ScrollChunksAsync(string relPath)
    {
        if (_qdrantGrpcUrl is null || Collection is null) return [];

        var uri = new Uri(_qdrantGrpcUrl);
        var grpcPort = uri.Port == 6333 ? 6334 : uri.Port;
        using var client = new QdrantClient(uri.Host, grpcPort);

        var result = new List<(string, string)>();
        PointId? offset = null;

        do
        {
            var response = await client.ScrollAsync(
                Collection,
                limit: 100,
                offset: offset,
                payloadSelector: new WithPayloadSelector { Enable = true });

            foreach (var pt in response.Result)
            {
                var payload = pt.Payload;
                var path = payload.TryGetValue("rel_path", out var rp) ? rp.StringValue : "";
                if (!path.Equals(relPath, StringComparison.OrdinalIgnoreCase)) continue;
                var bc   = payload.TryGetValue("breadcrumb", out var b)  ? b.StringValue  : "";
                var text = payload.TryGetValue("text",       out var t)  ? t.StringValue  : "";
                result.Add((bc, text));
            }

            offset = response.NextPageOffset;
        }
        while (offset is not null);

        return result;
    }

    public async Task DisposeAsync()
    {
        Store?.Dispose();
        _embedder?.Dispose();
        _workspace?.Dispose();
        if (_container is not null)
            await _container.DisposeAsync();
    }

    // ── Ingest helper ─────────────────────────────────────────────────────

    private static async Task IngestWorkspaceAsync(RagConfig cfg, OnnxEmbedder embedder, QdrantStore store)
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

                var docTitle = ExtractTitle(text, relPath);
                var points = new List<RagPoint>();

                for (var i = 0; i < chunks.Count; i += batchSize)
                {
                    var batch = chunks.Skip(i).Take(batchSize).ToList();
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
                                DocKind: "pattern",
                                AdrId: null,
                                Breadcrumb: chunk.Breadcrumb,
                                HeadingPath: chunk.HeadingPath,
                                StartLine: chunk.StartLine,
                                EndLine: chunk.EndLine,
                                TokenCount: chunk.TokenCount,
                                Weight: 1.0f,
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
            if (!string.IsNullOrEmpty(s) && !s.StartsWith('#') && !s.StartsWith("---")) break;
        }
        return relPath;
    }
}

/// <summary>
/// Temporary workspace for auto-mode E2E tests.
/// Contains two docs: the H4-sections fixture and the short-sections fixture.
/// Chunker config uses split_on_headings: "auto", min_tokens: 40.
/// </summary>
internal sealed class TempAutoModeWorkspace : IDisposable
{
    public string Root { get; }
    public string ConfigPath { get; }

    private TempAutoModeWorkspace(string root, string configPath)
    {
        Root = root;
        ConfigPath = configPath;
    }

    public static TempAutoModeWorkspace Create(string qdrantUrl, string collection, string modelDir)
    {
        var root = Path.Combine(Path.GetTempPath(), $"rag-auto-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        var ragDir = Path.Combine(root, "tools", "rag");
        Directory.CreateDirectory(ragDir);

        var configPath = Path.Combine(ragDir, "rag-config.yaml");
        File.WriteAllText(configPath, $"""
            source:
              roots:
                - docs
              exclude_globs: []
            embedder:
              model: "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2"
              dimensions: 384
              device: cpu
              batch_size: 32
              model_dir: "{modelDir.Replace('\\', '/')}"
            chunker:
              max_tokens: 800
              min_tokens: 40
              overlap_tokens: 0
              split_on_headings: "auto"
            vector_store:
              backend: qdrant
              collection: "{collection}"
              url: "{qdrantUrl}"
            ranking:
              weights: []
            query:
              top_k: 5
              score_threshold: 0.0
            storage:
              manifest_path: ".rag/manifest.json"
            """);

        File.WriteAllText(Path.Combine(ragDir, "metadata-rules.yaml"), """
            adr_id_patterns: []
            doc_kind_rules:
              - glob: "docs/**"
                kind: "pattern"
            """);

        var docsDir = Path.Combine(root, "docs");
        Directory.CreateDirectory(docsDir);

        File.WriteAllText(Path.Combine(docsDir, "auto-mode-h4-sections.md"),
            AutoModeE2EFixture.H4Doc);
        File.WriteAllText(Path.Combine(docsDir, "auto-mode-short-sections.md"),
            AutoModeE2EFixture.ShortSectionsDoc);

        return new TempAutoModeWorkspace(root, configPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch { /* best-effort */ }
    }
}
