using Microsoft.Extensions.Logging;
using RagTools.Core;
using System.Net.Http.Json;

// ── CLI entry point for incremental ingest ────────────────────────────────────
//
// Usage:
//   dotnet run                         # incremental — only changed/new files
//   dotnet run -- --force-full         # full rebuild, ignores manifest
//   dotnet run -- --dry-run            # print kind distribution without embedding
//   dotnet run -- -v                   # verbose output (LogLevel.Debug)
//   dotnet run -- --verbosity debug    # explicit level: trace|debug|information|warning|error
//   dotnet run -- --remote http://...  # push to remote HTTP server instead of embedding locally
//   dotnet run -- --api-key KEY        # X-Api-Key header for --remote (overrides RAG_API_KEY env)
//
// Environment variables:
//   RAG_WORKSPACE    absolute path to the repo root (default: cwd)
//   RAG_COLLECTION   Qdrant collection name override (default: from rag-config.yaml)
//   RAG_CONFIG       path to rag-config.yaml (default: /app/rag-config.yaml)
//   QDRANT_URL       Qdrant server URL (default: from rag-config.yaml)

var forceFull = args.Contains("--force-full");
var dryRun = args.Contains("--dry-run");
var verbose = args.Contains("-v") || args.Contains("--verbose");

// --remote <url>: push files to remote MCP server via HTTP instead of embedding locally.
string? remoteUrl = null;
string? apiKey = null;
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i] is "--remote")
    {
        remoteUrl = args[i + 1];
    }
    if (args[i] is "--api-key")
    {
        apiKey = args[i + 1];
    }
}
// Env var fallback for API key (mirrors Python --api-key / RAG_API_KEY).
apiKey ??= Environment.GetEnvironmentVariable("RAG_API_KEY");

// --verbosity <level> or -v shorthand
var logLevel = LogLevel.Information;  // default: show progress messages
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i] is "--verbosity" or "--log-level")
    {
        logLevel = args[i + 1].ToLowerInvariant() switch
        {
            "trace"       => LogLevel.Trace,
            "debug"       => LogLevel.Debug,
            "information" => LogLevel.Information,
            "info"        => LogLevel.Information,
            "warning"     => LogLevel.Warning,
            "warn"        => LogLevel.Warning,
            "error"       => LogLevel.Error,
            _             => LogLevel.Information,
        };
        break;
    }
}
if (verbose && logLevel > LogLevel.Debug)
    logLevel = LogLevel.Debug;

// Info and above always shown; Debug/Trace only when -v or --verbosity debug/trace.
using var loggerFactory = LoggerFactory.Create(b =>
    b.AddSimpleConsole(o =>
    {
        o.SingleLine = true;
        o.TimestampFormat = "HH:mm:ss ";
    })
    .SetMinimumLevel(logLevel));

var log = loggerFactory.CreateLogger("ingest");
var dbg = log;  // same logger — level controls what gets emitted

// Parse --config <path> argument.
string? configArgValue = null;
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i] is "--config")
    {
        configArgValue = args[i + 1];
        break;
    }
}

// Resolve config path via 3-way priority — --config arg > RAG_CONFIG env > default.
var resolvedConfigPath = RagConfig.ResolveConfigPath(configArgValue);

// Startup validation — fail fast on missing config / model so misconfiguration is obvious.
if (!File.Exists(resolvedConfigPath))
{
    log.LogError("rag-config.yaml not found at {Path}", resolvedConfigPath);
    log.LogError("Set RAG_CONFIG or RAG_WORKSPACE, or run from a directory that contains rag-config.yaml.");
    return 1;
}

var cfg = RagConfig.Load(resolvedConfigPath);

var repoRoot = cfg.Workspace;
var manifestPath = cfg.ManifestAbsPath;
var modelDirRaw = Environment.GetEnvironmentVariable("RAG_MODEL_DIR")
    ?? Path.Combine(AppContext.BaseDirectory, "model");
var modelDir = Path.IsPathRooted(modelDirRaw)
    ? modelDirRaw
    : Path.GetFullPath(Path.Combine(repoRoot, modelDirRaw));

var configFi = new FileInfo(resolvedConfigPath);
log.LogInformation("config     : {Path} ({Bytes} bytes)", resolvedConfigPath, configFi.Length);
log.LogInformation("repo root  : {Root}", repoRoot);
log.LogInformation("collection : {Collection}", cfg.Collection);
log.LogInformation("manifest   : {Manifest}", manifestPath);
log.LogInformation("model dir  : {ModelDir} (exists: {Exists})", modelDir, Directory.Exists(modelDir));
log.LogInformation("mode       : {Mode}", forceFull ? "full" : "incremental");

// Model dir is needed unless this is a --dry-run.
if (!dryRun && !Directory.Exists(modelDir))
{
    log.LogError("ONNX model directory not found at {ModelDir}", modelDir);
    log.LogError("Run: pwsh tools/rag-dotnet/download-model.ps1");
    return 1;
}

// ── Collect markdown files ────────────────────────────────────────────────────
var sourceRoots = cfg.Source.Roots
    .Select(r => Path.Combine(repoRoot, r))
    .Where(Directory.Exists)
    .ToList();

if (sourceRoots.Count == 0)
{
    log.LogError("No source roots found. Check rag-config.yaml source.roots.");
    return 1;
}

var allFiles = sourceRoots
    .SelectMany(root => Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
    .Select(f => new FileInfo(f))
    .Where(fi => !IsExcluded(fi, repoRoot, cfg.Source.ExcludeGlobs))
    .OrderBy(fi => fi.FullName)
    .ToList();

log.LogInformation("found {Count} markdown files", allFiles.Count);

// ── Dry run: print kind distribution ─────────────────────────────────────────
if (dryRun)
{
    var kindCounts = new Dictionary<string, int>();
    foreach (var fi in allFiles)
    {
        var rel = Path.GetRelativePath(repoRoot, fi.FullName).Replace('\\', '/');
        var kind = cfg.DetectDocKind(rel);
        kindCounts[kind] = kindCounts.GetValueOrDefault(kind) + 1;
    }
    log.LogInformation("[dry-run] document kind distribution:");
    foreach (var (kind, count) in kindCounts.OrderByDescending(kv => kv.Value))
        log.LogInformation("  {Kind,-30} {Count,5} files", kind, count);
    return 0;
}

// ── Load manifest (incremental) ───────────────────────────────────────────────
var manifest = forceFull
    ? ManifestService.CreateEmpty(manifestPath) // skip reading, save to correct path
    : ManifestService.Load(manifestPath);

// Determine which files need processing.
var toProcess = new List<(FileInfo File, string RelPath, string Hash)>();
foreach (var fi in allFiles)
{
    var rel = Path.GetRelativePath(repoRoot, fi.FullName).Replace('\\', '/');
    var hash = ManifestService.HashFile(fi.FullName)!;
    if (!forceFull && manifest.IsUnchanged(rel, hash))
        continue;
    toProcess.Add((fi, rel, hash));
}

// Find deleted files (in manifest but not on disk).
var currentRelPaths = allFiles.Select(fi => Path.GetRelativePath(repoRoot, fi.FullName).Replace('\\', '/')).ToList();
var deleted = manifest.FindDeleted(currentRelPaths).ToList();

log.LogInformation("to process : {Count} file(s) (changed/new)", toProcess.Count);
log.LogInformation("to delete  : {Count} file(s) (removed from disk)", deleted.Count);

if (toProcess.Count == 0 && deleted.Count == 0)
{
    log.LogInformation("nothing to do — index is up to date");
    return 0;
}

// ── Remote mode: POST files to MCP server instead of embedding locally ────────
if (remoteUrl is not null)
{
    log.LogInformation("remote mode: uploading {Count} file(s) to {Url}", toProcess.Count, remoteUrl);
    using var remoteClient = new RemoteIngestClient(remoteUrl, apiKey, log);
    var failCount = await remoteClient.PushAsync(cfg, toProcess, manifest);
    manifest.Save();
    return failCount > 0 ? 1 : 0;
}

// ── Load embedder + Qdrant ────────────────────────────────────────────────────
log.LogInformation("loading ONNX embedder ...");
using var embedder = OnnxEmbedder.Load(modelDir);
log.LogInformation("embedding dimensions: {Dims}", embedder.Dimensions);

var qdrantUrl = Environment.GetEnvironmentVariable("QDRANT_URL") ?? cfg.QdrantUrl;
using var store = new QdrantDocumentStore(qdrantUrl);
if (forceFull)
    await store.RecreateCollectionAsync(cfg.Collection, embedder.Dimensions);
else
    await store.EnsureCollectionAsync(cfg.Collection, embedder.Dimensions);

// ── Delete removed files ──────────────────────────────────────────────────────
if (deleted.Count > 0)
{
    log.LogInformation("deleting {Count} stale points ...", deleted.Count);
    await store.DeleteByPathsAsync(cfg.Collection, deleted);
    foreach (var rel in deleted)
    {
        dbg.LogDebug("deleted: {RelPath}", rel);
        manifest.Remove(rel);
    }
}

// ── Chunk, embed, upsert ──────────────────────────────────────────────────────
var tokenCounter = SentencePieceTokenCounter.FromModelDir(modelDir);
var chunker = new MarkdownChunker(cfg.Chunker, tokenCounter);
var processorLogger = loggerFactory.CreateLogger<DocumentProcessor>();
var processor = new DocumentProcessor(cfg, chunker, embedder, store, processorLogger);
var ingestor = new FileIngestor(processor, cfg, manifest, log);
var totalChunks = await ingestor.IngestAsync(toProcess);

manifest.Save();
WriteStatsMd(cfg, manifest, repoRoot);
log.LogInformation("done — {Files} file(s), {Chunks} chunks, manifest saved", toProcess.Count, totalChunks);
return 0;

// ── Local helpers ─────────────────────────────────────────────────────────────

static bool IsExcluded(FileInfo fi, string repoRoot, IEnumerable<string> globs)
{
    var rel = Path.GetRelativePath(repoRoot, fi.FullName).Replace('\\', '/');
    return globs.Any(g => FileIngestor.GlobMatch(rel, g));
}

static void WriteStatsMd(RagConfig cfg, ManifestService manifest, string repoRoot)
{
    const string statsRel = "docs/rag/index-stats-dotnet.md";
    var statsPath = Path.Combine(repoRoot, statsRel);

    // Aggregate from manifest entries.
    var kindChunks = new Dictionary<string, int>();
    var kindFiles = new Dictionary<string, HashSet<string>>();
    var fileChunks = new Dictionary<string, int>();
    var fileKind = new Dictionary<string, string>();

    foreach (var (relPath, entry) in manifest.All())
    {
        var kind = cfg.DetectDocKind(relPath);
        kindChunks[kind] = kindChunks.GetValueOrDefault(kind) + entry.ChunkCount;
        kindFiles.TryAdd(kind, []);
        kindFiles[kind].Add(relPath);
        fileChunks[relPath] = entry.ChunkCount;
        fileKind[relPath] = kind;
    }

    var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC";
    var totalFiles = manifest.FileCount;
    var totalChunks = kindChunks.Values.Sum();

    var sb = new System.Text.StringBuilder();
    sb.AppendLine("# RAG Index Stats");
    sb.AppendLine();
    sb.AppendLine($"Last indexed: {now}  ");
    sb.AppendLine($"Collection: `{cfg.Collection}`  ");
    sb.AppendLine($"Files: {totalFiles}  ");
    sb.AppendLine($"Chunks: {totalChunks}  ");
    sb.AppendLine();
    sb.AppendLine("## Breakdown by doc_kind");
    sb.AppendLine();
    sb.AppendLine("| doc_kind | files | chunks |");
    sb.AppendLine("|----------|------:|-------:|");
    foreach (var kind in kindChunks.Keys.Order())
        sb.AppendLine($"| `{kind}` | {kindFiles[kind].Count} | {kindChunks[kind]} |");

    sb.AppendLine();
    sb.AppendLine("## Per-file detail");
    sb.AppendLine();
    sb.AppendLine("| file | doc_kind | chunks |");
    sb.AppendLine("|------|----------|-------:|");
    foreach (var rel in fileChunks.Keys.Order())
        sb.AppendLine($"| `{rel}` | `{fileKind[rel]}` | {fileChunks[rel]} |");

    sb.AppendLine();

    Directory.CreateDirectory(Path.GetDirectoryName(statsPath)!);
    File.WriteAllText(statsPath, sb.ToString(), System.Text.Encoding.UTF8);
    Console.WriteLine($"[ingest] stats written: {statsRel}");
}
