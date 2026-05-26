using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using RagTools.Core;
using RagTools.Core.ContentSources;
using RagTools.Core.Ingest;
using RagTools.Mcp;
using RagTools.Mcp.Controllers;
using RagTools.Mcp.Middleware;
using System.Text.Json;

// ── MCP server entry point ────────────────────────────────────────────────────
//
// Supports two transports, selected via the MCP_TRANSPORT environment variable:
//
//   MCP_TRANSPORT=stdio  (default)
//     VS Code / Copilot spawns this process and communicates over stdin/stdout.
//     Uses Host.CreateApplicationBuilder — stdout stays clean for MCP framing.
//
//   MCP_TRANSPORT=http  (also accepts: sse — legacy alias)
//     Runs a persistent Streamable HTTP server.
//     VS Code connects via mcp.json "type":"http", "url":"http://host:PORT/".
//     Uses WebApplication.CreateBuilder (ASP.NET Core / Kestrel).
//
// Environment variables:
//   MCP_TRANSPORT    "stdio" (default) or "http" (legacy alias: "sse")
//   MCP_PORT         port for SSE mode (default: 3001)
//   MCP_LOG_LEVEL    log verbosity: trace|debug|information|warning|error (default: warning)
//   RAG_WORKSPACE    absolute path to the repo root
//   RAG_COLLECTION   Qdrant collection name override
//   RAG_CONFIG       path to rag-config.yaml (default: /app/rag-config.yaml)
//   QDRANT_URL       Qdrant server URL (default: from rag-config.yaml)
//   RAG_MODEL_DIR    path to ONNX model directory (default: /app/model)
//
// CLI flags:
//   -v / --verbose            shorthand for debug level
//   --verbosity <level>       trace|debug|information|warning|error

var transport = (Environment.GetEnvironmentVariable("MCP_TRANSPORT") ?? "stdio").ToLowerInvariant();
var port = int.TryParse(Environment.GetEnvironmentVariable("MCP_PORT"), out var p) ? p : 3001;

// ── Log level resolution: CLI flag > MCP_LOG_LEVEL env var > default (Warning)
var logLevel = ParseLogLevel(
    args,
    Environment.GetEnvironmentVariable("MCP_LOG_LEVEL"),
    args.Contains("-v") || args.Contains("--verbose") ? LogLevel.Debug : LogLevel.Warning);

static LogLevel ParseLogLevel(string[] args, string? envVal, LogLevel fallback)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (args[i] is "--verbosity" or "--log-level")
            return MapLevel(args[i + 1], fallback);
    return envVal is not null ? MapLevel(envVal, fallback) : fallback;
}

static LogLevel MapLevel(string s, LogLevel fallback) => s.ToLowerInvariant() switch
{
    "trace"       => LogLevel.Trace,
    "debug"       => LogLevel.Debug,
    "information" => LogLevel.Information,
    "info"        => LogLevel.Information,
    "warning"     => LogLevel.Warning,
    "warn"        => LogLevel.Warning,
    "error"       => LogLevel.Error,
    _             => fallback,
};

// Resolve config via 3-way priority (--config not supported here; MCP runs headless).
var resolvedConfigPath = RagConfig.ResolveConfigPath(null);

if (!File.Exists(resolvedConfigPath))
{
    Console.Error.WriteLine($"[rag-mcp] ERROR: rag-config.yaml not found at {resolvedConfigPath}");
    Console.Error.WriteLine("[rag-mcp] Set RAG_CONFIG or RAG_WORKSPACE before launching the MCP server.");
    return 1;
}

var cfg = RagConfig.Load(resolvedConfigPath);
var qdrantUrl = Environment.GetEnvironmentVariable("QDRANT_URL") ?? cfg.QdrantUrl;
var modelDirRaw = Environment.GetEnvironmentVariable("RAG_MODEL_DIR")
    ?? Path.Combine(AppContext.BaseDirectory, "model");
var modelDir = Path.IsPathRooted(modelDirRaw)
    ? modelDirRaw
    : Path.GetFullPath(Path.Combine(cfg.Workspace, modelDirRaw));

// Startup log — always to stderr so it never corrupts stdio MCP framing.
var configFi = new FileInfo(resolvedConfigPath);
Console.Error.WriteLine($"[rag-mcp] transport  : {transport}{(transport == "sse" ? $" (port {port})" : "")}");
Console.Error.WriteLine($"[rag-mcp] config     : {resolvedConfigPath} ({configFi.Length} bytes)");
Console.Error.WriteLine($"[rag-mcp] workspace  : {cfg.Workspace}");
Console.Error.WriteLine($"[rag-mcp] collection : {cfg.Collection}");
Console.Error.WriteLine($"[rag-mcp] qdrant     : {qdrantUrl}");
Console.Error.WriteLine($"[rag-mcp] embedder   : onnx");
Console.Error.WriteLine($"[rag-mcp] model dir  : {modelDir} (exists: {Directory.Exists(modelDir)})");

if (!Directory.Exists(modelDir))
{
    Console.Error.WriteLine($"[rag-mcp] ERROR: ONNX model directory not found at {modelDir}");
    Console.Error.WriteLine("[rag-mcp] Run: pwsh tools/rag-dotnet/download-model.ps1");
    return 1;
}

if (transport is "http" or "sse")
{
    // ── SSE / HTTP mode ───────────────────────────────────────────────────────
    // Uses WebApplication (Kestrel). Stdout is safe for normal logs here.
    var webBuilder = WebApplication.CreateBuilder(args);
    webBuilder.Logging.SetMinimumLevel(logLevel);

    // Wire ONNX embedder pipeline.
    webBuilder.Services.AddOnnxEmbedderPipeline(modelDir).Register();

    webBuilder.Services
        .AddControllers()
        .AddJsonOptions(opts =>
            opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower)
        .Services
        .AddRouting(opts =>
            opts.ConstraintMap["collection"] = typeof(RagTools.Mcp.Routing.CollectionNameRouteConstraint))
        .AddSingleton(cfg)
        .AddSingleton<ITokenCounter>(_ => SentencePieceTokenCounter.FromModelDir(modelDir))
        .AddSingleton<IDocumentStore>(_ =>
            new CachedDocumentStore(
                new QdrantDocumentStore(qdrantUrl),
                new QueryCache()))
        .AddSingleton<MarkdownChunker>(sp =>
            new MarkdownChunker(cfg.Chunker, sp.GetRequiredService<ITokenCounter>()))
        .AddSingleton<IDocumentProcessor, DocumentProcessor>()
        .AddSingleton<IngestChannel>()
        .AddSingleton<OperationStore>()
        .AddSingleton<IBatchIngestService, BatchIngestService>()
        .AddSingleton<BatchValidator>()
        .AddSingleton<IZipBatchParser, ZipBatchParser>()
        .AddHostedService<IngestWorker>()
        // D-6 fix: ICollectionResolver reads the live request via IHttpContextAccessor AsyncLocal —
        // correctly propagated into MCP inner DI scopes. RagSession + IContentSource are Singletons.
        .AddHttpContextAccessor()
        .AddSingleton<ICollectionResolver, HttpCollectionResolver>()
        .AddSingleton<RagSession>()
        .AddSingleton<IContentSource, QdrantContentSource>()
        .AddSingleton<RagTools.Core.Query.IRagQueryService, RagTools.Core.Query.RagQueryService>()
        .AddSingleton<RagTools.Core.ReadDocs.IRagReadDocsService, RagTools.Core.ReadDocs.RagReadDocsService>()
        .AddSingleton<RagTools.Core.History.IRagHistoryService, RagTools.Core.History.RagHistoryService>()
        .AddSingleton<RagTools.Core.Adrs.IRagListService, RagTools.Core.Adrs.RagListService>()
        .AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly();

    var app = webBuilder.Build();
    app.UseMiddleware<ApiKeyMiddleware>();
    // Convert malformed JSON-RPC / oversize bodies into a JSON 400 envelope
    // instead of leaking an HTML 500 page or framework exception text.
    app.Use(async (ctx, next) =>
    {
        try
        {
            await next();
        }
        catch (Microsoft.AspNetCore.Http.BadHttpRequestException ex)
        {
            ctx.Response.Clear();
            ctx.Response.StatusCode = ex.StatusCode is >= 400 and < 500 ? ex.StatusCode : 400;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(
                "{\"error\":\"Malformed request body.\",\"code\":\"BadRequest\"}");
        }
        catch (System.Text.Json.JsonException)
        {
            ctx.Response.Clear();
            ctx.Response.StatusCode = 400;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(
                "{\"error\":\"Malformed JSON payload.\",\"code\":\"BadRequest\"}");
        }
    });
    app.MapControllers();
    app.MapMcp("/");
    Console.Error.WriteLine($"[rag-mcp] endpoint  : http://0.0.0.0:{port}/ (MCP Streamable HTTP)");
    await app.RunAsync($"http://0.0.0.0:{port}");
}
else
{
    // ── stdio mode (default) ──────────────────────────────────────────────────
    // Uses Host — stdout must stay 100% clean for MCP JSON framing.
    var builder = Host.CreateApplicationBuilder(args);

    // Host.CreateApplicationBuilder adds a Console logger that writes to stdout by
    // default.  In stdio mode that corrupts the MCP JSON stream.  Clear it and
    // add a stderr-only provider so diagnostic logs remain visible without
    // polluting the protocol channel.
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(opts => opts.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Logging.SetMinimumLevel(logLevel);

    // Wire ONNX embedder pipeline.
    builder.Services.AddOnnxEmbedderPipeline(modelDir).Register();

    builder.Services
        .AddSingleton(cfg)
        .AddSingleton<IDocumentStore>(_ =>
            new CachedDocumentStore(
                new QdrantDocumentStore(qdrantUrl),
                new QueryCache()))
        // STDIO: one collection per process — resolve from env var or config default.
        .AddSingleton<ICollectionResolver>(_ =>
            new FixedCollectionResolver(
                Environment.GetEnvironmentVariable("RAG_COLLECTION") ?? cfg.Collection))
        .AddSingleton<RagSession>()
        .AddSingleton<IContentSource, DiskContentSource>()
        .AddSingleton<RagTools.Core.Query.IRagQueryService, RagTools.Core.Query.RagQueryService>()
        .AddSingleton<RagTools.Core.ReadDocs.IRagReadDocsService, RagTools.Core.ReadDocs.RagReadDocsService>()
        .AddSingleton<RagTools.Core.History.IRagHistoryService, RagTools.Core.History.RagHistoryService>()
        .AddSingleton<RagTools.Core.Adrs.IRagListService, RagTools.Core.Adrs.RagListService>()
        .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

    var host = builder.Build();
    await host.RunAsync();
}
return 0;

