using RagTools.Core;

// ── CLI query tool — mirrors Python's query.py ────────────────────────────────
//
// Usage:
//   dotnet run -- "query text" [--top-k 5]
//
// Environment variables (same as MCP / Ingest):
//   RAG_WORKSPACE, RAG_COLLECTION, RAG_CONFIG, QDRANT_URL, RAG_MODEL_DIR

if (args.Length == 0 || args[0] == "--help")
{
    Console.Error.WriteLine("Usage: dotnet run -- \"query text\" [--top-k N]");
    return 1;
}

var query = args[0];
var topK = 5;
for (var i = 1; i < args.Length - 1; i++)
    if (args[i] == "--top-k" && int.TryParse(args[i + 1], out var k)) topK = k;

var resolvedConfigPath = RagConfig.ResolveConfigPath(null);
if (!File.Exists(resolvedConfigPath))
{
    Console.Error.WriteLine($"[query] config not found: {resolvedConfigPath}");
    return 1;
}

var cfg = RagConfig.Load(resolvedConfigPath);
var qdrantUrl = Environment.GetEnvironmentVariable("QDRANT_URL") ?? cfg.QdrantUrl;
var modelDir = Environment.GetEnvironmentVariable("RAG_MODEL_DIR")
    ?? Path.Combine(AppContext.BaseDirectory, "model");

Console.Error.WriteLine($"[query] collection: {cfg.Collection}  qdrant: {qdrantUrl}");

using var embedder = OnnxEmbedder.Load(modelDir);
using var store = QdrantStore.Connect(qdrantUrl, cfg.Collection);

var threshold = cfg.Query.ScoreThreshold;

var vector = embedder.Embed(query);
var results = await store.SearchAsync(vector, topK, threshold);

if (results.Count == 0)
{
    Console.WriteLine($"(no results above threshold {threshold:F2})");
    return 0;
}

for (var i = 0; i < results.Count; i++)
{
    var hit = results[i];
    var preview = string.Join(" ", hit.Text.Trim().Split('\n').Select(l => l.Trim()));
    if (preview.Length > 240) preview = preview[..240];
    Console.WriteLine($"#{i + 1}  score={hit.Score:F3}  raw={hit.Score:F3}");
    Console.WriteLine($"     {hit.RelPath}:{hit.StartLine}");
    Console.WriteLine($"     {hit.Breadcrumb}");
    Console.WriteLine($"     > {preview}...");
    Console.WriteLine();
}
return 0;

