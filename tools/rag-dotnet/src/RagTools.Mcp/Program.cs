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

// ── Background startup sync ───────────────────────────────────────────────────
// Mirrors Python mcp_server.py _startup_check(): compare file hashes from manifest
// against current disk state and spawn the ingest tool if any file changed.
// Runs in a daemon thread so it never blocks the first tool call.
//
// RAG_INGEST_DLL env var: absolute path to the compiled ingest.dll (auto-detected
// from AppContext.BaseDirectory when not set). Falls back to no-op if not found.
_ = Task.Run(() =>
{
    try
    {
        var manifestPath = cfg.ManifestAbsPath;
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine("[rag-mcp] No manifest found — skipping startup sync. Run ingest first.");
            return;
        }

        // Read file_hashes from the manifest (Python format: flat dict at top level or under "file_hashes").
        var manifestJson = File.ReadAllText(manifestPath);
        using var doc = System.Text.Json.JsonDocument.Parse(manifestJson);
        var root = doc.RootElement;

        // Python manifest format: root has "file_hashes" key with {relPath: sha256} pairs.
        if (!root.TryGetProperty("file_hashes", out var fileHashesEl) || fileHashesEl.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            // .NET manifest format: root is {relPath: {hash, ...}} dict — read directly.
            fileHashesEl = root;
        }

        var storedHashes = fileHashesEl.EnumerateObject()
            .ToDictionary(p => p.Name, p =>
                p.Value.ValueKind == System.Text.Json.JsonValueKind.String
                    ? p.Value.GetString() ?? ""
                    : p.Value.TryGetProperty("hash", out var h) ? h.GetString() ?? "" : "");

        if (storedHashes.Count == 0)
        {
            Console.Error.WriteLine("[rag-mcp] Manifest has no file hashes — skipping startup sync.");
            return;
        }

        // Scan current docs and compare hashes.
        var workspace = cfg.Workspace;
        var changed = new List<string>();
        var currentRels = new HashSet<string>();
        foreach (var root2 in cfg.Source.Roots)
        {
            var absRoot = Path.IsPathRooted(root2) ? root2 : Path.Combine(workspace, root2);
            if (!Directory.Exists(absRoot)) continue;
            foreach (var file in Directory.EnumerateFiles(absRoot, "*.md", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(workspace, file).Replace('\\', '/');
                currentRels.Add(rel);
                var hash = ManifestService.HashFile(file);
                if (hash is null) continue;
                if (!storedHashes.TryGetValue(rel, out var stored) || stored != hash)
                    changed.Add(rel);
            }
        }
        // Detect deleted files.
        foreach (var rel in storedHashes.Keys)
            if (!currentRels.Contains(rel))
                changed.Add(rel);

        if (changed.Count == 0)
        {
            Console.Error.WriteLine("[rag-mcp] Index up to date — no startup sync needed.");
            return;
        }

        Console.Error.WriteLine($"[rag-mcp] {changed.Count} file(s) changed — running incremental ingest in background...");

        // Resolve ingest executable: RAG_INGEST_DLL > sibling ingest/ dir > skip.
        var ingestDll = Environment.GetEnvironmentVariable("RAG_INGEST_DLL");
        if (string.IsNullOrEmpty(ingestDll))
        {
            // When published, ingest.dll sits alongside mcp_server.dll in the same output tree.
            var siblingIngest = Path.Combine(AppContext.BaseDirectory, "ingest", "ingest.dll");
            if (File.Exists(siblingIngest)) ingestDll = siblingIngest;
        }
        if (string.IsNullOrEmpty(ingestDll))
        {
            Console.Error.WriteLine("[rag-mcp] RAG_INGEST_DLL not set and ingest.dll not found — skipping auto-sync. Run ingest manually.");
            return;
        }

        using var proc = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList = { ingestDll, "--config", resolvedConfigPath },
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                Environment =
                {
                    ["QDRANT_URL"] = qdrantUrl,
                    ["RAG_COLLECTION"] = cfg.Collection,
                    ["RAG_MODEL_DIR"] = modelDir,
                    ["RAG_WORKSPACE"] = workspace,
                },
            },
        };
        proc.Start();
        // Timeout: 600s — matches Python. If exceeded, existing index is still usable.
        if (!proc.WaitForExit(TimeSpan.FromSeconds(600)))
        {
            Console.Error.WriteLine("[rag-mcp] Incremental ingest is still running after 600s — existing index remains usable.");
        }
        else if (proc.ExitCode != 0)
        {
            Console.Error.WriteLine($"[rag-mcp] WARNING: Incremental ingest failed (exit {proc.ExitCode}). Run ingest manually.");
        }
        else
        {
            Console.Error.WriteLine("[rag-mcp] Startup sync complete.");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[rag-mcp] Startup sync error: {ex.Message}");
    }
});

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
