using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using RagTools.Core;

// ── MCP server entry point ────────────────────────────────────────────────────
//
// Runs as a stdio MCP server. VS Code / Copilot spawns this process and
// communicates over stdin/stdout.
//
// Environment variables:
//   RAG_WORKSPACE    absolute path to the repo root
//   RAG_COLLECTION   Qdrant collection name override
//   RAG_CONFIG       path to config.yaml (default: /app/config.yaml)
//   QDRANT_URL       Qdrant server URL (default: from config.yaml)
//   RAG_MODEL_DIR    path to ONNX model directory (default: /app/model)

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
var modelDir = Environment.GetEnvironmentVariable("RAG_MODEL_DIR")
    ?? Path.Combine(AppContext.BaseDirectory, "model");

// Startup log — printed to stderr so it doesn't corrupt the stdio MCP framing on stdout.
var configFi = new FileInfo(resolvedConfigPath);
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

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddSingleton(cfg)
    .AddSingleton(_ => OnnxEmbedder.Load(modelDir))
    .AddSingleton(_ => QdrantStore.Connect(qdrantUrl, cfg.Collection))
    .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

var host = builder.Build();
await host.RunAsync();
return 0;
