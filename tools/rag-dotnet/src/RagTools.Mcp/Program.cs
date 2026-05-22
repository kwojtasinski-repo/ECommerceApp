using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using RagTools.Core;
using RagTools.Mcp.Controllers;
using RagTools.Mcp.Middleware;

// ── MCP server entry point ────────────────────────────────────────────────────
//
// Supports two transports, selected via the MCP_TRANSPORT environment variable:
//
//   MCP_TRANSPORT=stdio  (default)
//     VS Code / Copilot spawns this process and communicates over stdin/stdout.
//     Uses Host.CreateApplicationBuilder — stdout stays clean for MCP framing.
//
//   MCP_TRANSPORT=sse
//     Runs a persistent HTTP server with /sse endpoint.
//     VS Code connects via mcp.json "type":"sse", "url":"http://host:PORT/sse".
//     Uses WebApplication.CreateBuilder (ASP.NET Core / Kestrel).
//
// Environment variables:
//   MCP_TRANSPORT    "stdio" (default) or "sse"
//   MCP_PORT         port for SSE mode (default: 3001)
//   MCP_LOG_LEVEL    log verbosity: trace|debug|information|warning|error (default: warning)
//   RAG_WORKSPACE    absolute path to the repo root
//   RAG_COLLECTION   Qdrant collection name override
//   RAG_CONFIG       path to config.yaml (default: /app/config.yaml)
//   QDRANT_URL       Qdrant server URL (default: from config.yaml)
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
    Console.Error.WriteLine($"[rag-mcp] ERROR: config.yaml not found at {resolvedConfigPath}");
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
Console.Error.WriteLine($"[rag-mcp] model dir  : {modelDir} (exists: {Directory.Exists(modelDir)})");

if (!Directory.Exists(modelDir))
{
    Console.Error.WriteLine($"[rag-mcp] ERROR: ONNX model directory not found at {modelDir}");
    Console.Error.WriteLine("[rag-mcp] Run: pwsh tools/rag-dotnet/download-model.ps1");
    return 1;
}

if (transport == "sse")
{
    // ── SSE / HTTP mode ───────────────────────────────────────────────────────
    // Uses WebApplication (Kestrel). Stdout is safe for normal logs here.
    var webBuilder = WebApplication.CreateBuilder(args);
    webBuilder.Logging.SetMinimumLevel(logLevel);
    webBuilder.Services
        .AddControllers()
        .Services
        .AddSingleton(cfg)
        .AddSingleton(_ => OnnxEmbedder.Load(modelDir))
        .AddSingleton<ITokenCounter>(_ => SentencePieceTokenCounter.FromModelDir(modelDir))
        .AddSingleton<IDocumentStore>(_ =>
            new CachedDocumentStore(
                new QdrantDocumentStore(qdrantUrl),
                new QueryCache()))
        .AddSingleton<IngestChannel>()
        .AddSingleton<OperationStore>()
        .AddHostedService<IngestWorker>()
        .AddScoped<RagSession>()
        .AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly();

    var app = webBuilder.Build();
    app.UseMiddleware<ApiKeyMiddleware>();
    app.UseMiddleware<RagSessionMiddleware>();
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

    builder.Services
        .AddSingleton(cfg)
        .AddSingleton(_ => OnnxEmbedder.Load(modelDir))
        .AddSingleton<IDocumentStore>(_ =>
            new CachedDocumentStore(
                new QdrantDocumentStore(qdrantUrl),
                new QueryCache()))
        // In stdio mode there is no HTTP context — RagSession is a singleton
        // using cfg.Collection as the fixed collection for this process.
        .AddSingleton<RagSession>()
        .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

    var host = builder.Build();
    await host.RunAsync();
}
return 0;

