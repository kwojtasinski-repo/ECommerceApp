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

var configPath = Environment.GetEnvironmentVariable("RAG_CONFIG")
    ?? Path.Combine(AppContext.BaseDirectory, "config.yaml");

var cfg = RagConfig.Load(configPath);
var qdrantUrl = Environment.GetEnvironmentVariable("QDRANT_URL") ?? cfg.QdrantUrl;
var modelDir = Environment.GetEnvironmentVariable("RAG_MODEL_DIR")
    ?? Path.Combine(AppContext.BaseDirectory, "model");

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
