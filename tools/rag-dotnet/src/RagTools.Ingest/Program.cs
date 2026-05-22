using Microsoft.Extensions.Logging;
using RagTools.Core;
using System.Net.Http.Json;

// ── CLI entry point for incremental ingest ────────────────────────────────────
//
// Usage:
//   dotnet run                       # incremental — only changed/new files
//   dotnet run -- --force-full       # full rebuild, ignores manifest
//   dotnet run -- --dry-run          # print kind distribution without embedding
//   dotnet run -- -v                 # verbose output (LogLevel.Debug)
//   dotnet run -- --verbosity debug  # explicit level: trace|debug|information|warning|error
//
// Environment variables:
//   RAG_WORKSPACE    absolute path to the repo root (default: cwd)
//   RAG_COLLECTION   Qdrant collection name override (default: from config.yaml)
//   RAG_CONFIG       path to config.yaml (default: /app/config.yaml)
//   QDRANT_URL       Qdrant server URL (default: from config.yaml)

var forceFull = args.Contains("--force-full");
var dryRun = args.Contains("--dry-run");
var verbose = args.Contains("-v") || args.Contains("--verbose");

// --remote <url>: push files to remote MCP server via HTTP instead of embedding locally.
string? remoteUrl = null;
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i] is "--remote")
    {
        remoteUrl = args[i + 1];
        break;
    }
}

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
    log.LogError("config.yaml not found at {Path}", resolvedConfigPath);
    log.LogError("Set RAG_CONFIG or RAG_WORKSPACE, or run from a directory that contains config.yaml.");
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
    log.LogError("No source roots found. Check config.yaml source.roots.");
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
    var apiKey = Environment.GetEnvironmentVariable("RAG_API_KEY");
    using var http = new HttpClient();
    http.BaseAddress = new Uri(remoteUrl.TrimEnd('/') + "/");
    if (!string.IsNullOrEmpty(apiKey))
        http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

    var collection = cfg.Collection;
    var processed = 0;
    var failed = 0;

    foreach (var (fi, relPath, hash) in toProcess)
    {
        var content = await File.ReadAllTextAsync(fi.FullName);
        var payload = new
        {
            rel_path = relPath,
            content,
            doc_kind = (string?)null,  // auto-detect on server
        };

        try
        {
            var response = await http.PostAsJsonAsync($"ingest/{Uri.EscapeDataString(collection)}", payload);
            if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                log.LogWarning("Server queue full for {RelPath}, retrying in 2s ...", relPath);
                await Task.Delay(2000);
                response = await http.PostAsJsonAsync($"ingest/{Uri.EscapeDataString(collection)}", payload);
            }
            response.EnsureSuccessStatusCode();
            manifest.Update(relPath, hash, 0);  // chunk count unknown in remote mode
            processed++;
            dbg.LogDebug("queued {RelPath}", relPath);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "failed to upload {RelPath}", relPath);
            failed++;
        }
    }

    manifest.Save();
    log.LogInformation("remote ingest: {Processed} queued, {Failed} failed", processed, failed);
    return failed > 0 ? 1 : 0;
}

// ── Load embedder + Qdrant ────────────────────────────────────────────────────
log.LogInformation("loading ONNX embedder ...");
using var embedder = OnnxEmbedder.Load(modelDir);
log.LogInformation("embedding dimensions: {Dims}", embedder.Dimensions);

var qdrantUrl = Environment.GetEnvironmentVariable("QDRANT_URL") ?? cfg.QdrantUrl;
using var store = QdrantStore.Connect(qdrantUrl, cfg.Collection);
if (forceFull)
    await store.RecreateCollectionAsync(embedder.Dimensions);
else
    await store.EnsureCollectionAsync(embedder.Dimensions);

// ── Delete removed files ──────────────────────────────────────────────────────
if (deleted.Count > 0)
{
    log.LogInformation("deleting {Count} stale points ...", deleted.Count);
    await store.DeleteByPathsAsync(deleted);
    foreach (var rel in deleted)
    {
        dbg.LogDebug("deleted: {RelPath}", rel);
        manifest.Remove(rel);
    }
}

// ── Chunk, embed, upsert ──────────────────────────────────────────────────────
var tokenCounter = SentencePieceTokenCounter.FromModelDir(modelDir);
var chunker = new MarkdownChunker(cfg.Chunker, tokenCounter);
var batchSize = cfg.Embedder.BatchSize;

var totalChunks = 0;
var processedFiles = 0;

foreach (var (fi, relPath, hash) in toProcess)
{
    var text = await File.ReadAllTextAsync(fi.FullName);
    var chunks = chunker.Chunk(text, relPath);
    var docTitle = ExtractTitle(text, relPath);
    var weight = ResolveWeight(relPath, (int)fi.Length, cfg.Ranking);
    var kind = cfg.DetectDocKind(relPath);
    var adrId = cfg.DetectAdrId(relPath);

    // Delete old points for this file before re-upserting.
    await store.DeleteByPathsAsync([relPath]);

    // Embed in batches.
    var points = new List<RagPoint>();
    for (var i = 0; i < chunks.Count; i += batchSize)
    {
        var batch = chunks.Skip(i).Take(batchSize).ToList();
        var texts = batch.Select(c => c.Breadcrumb + "\n\n" + c.Text).ToList();
        var vectors = embedder.EmbedBatch(texts);

        for (var j = 0; j < batch.Count; j++)
        {
            var chunk = batch[j];
            var chunkIndex = i + j;   // global chunk index across batches
            var id = ManifestService.StableId(relPath, chunk.Breadcrumb, chunk.StartLine);
            var contentId = DeterministicId.ForContent(cfg.Collection, relPath);
            points.Add(new RagPoint(id, vectors[j], new RagPayload(
                RelPath: relPath,
                DocTitle: docTitle,
                DocKind: kind,
                AdrId: adrId,
                Breadcrumb: chunk.Breadcrumb,
                HeadingPath: chunk.HeadingPath,
                StartLine: chunk.StartLine,
                EndLine: chunk.EndLine,
                TokenCount: chunk.TokenCount,
                Weight: weight,
                Text: chunk.Text,
                ChunkIndex: chunkIndex,
                ContentId: contentId)));
        }
    }

    await store.UpsertAsync(points);
    manifest.Update(relPath, hash, chunks.Count);
    totalChunks += chunks.Count;
    processedFiles++;

    dbg.LogDebug("{RelPath}: {ChunkCount} chunks, kind={Kind}, weight={Weight}", relPath, chunks.Count, kind, weight);
    if (processedFiles % 10 == 0)
        log.LogInformation("{Done}/{Total} files processed ...", processedFiles, toProcess.Count);
}

manifest.Save();
WriteStatsMd(cfg, manifest, repoRoot);
log.LogInformation("done — {Files} file(s), {Chunks} chunks, manifest saved", processedFiles, totalChunks);
return 0;

// ── Local helpers ─────────────────────────────────────────────────────────────

static bool IsExcluded(FileInfo fi, string repoRoot, IEnumerable<string> globs)
{
    var rel = Path.GetRelativePath(repoRoot, fi.FullName).Replace('\\', '/');
    return globs.Any(g => GlobMatch(rel, g));
}

static bool GlobMatch(string path, string glob)
{
    var pattern = "^" +
        System.Text.RegularExpressions.Regex.Escape(glob)
             .Replace(@"\*\*", "§§")
             .Replace(@"\*", "[^/]*")
             .Replace(@"\?", "[^/]")
             .Replace("§§", ".*")
        + "$";
    return System.Text.RegularExpressions.Regex.IsMatch(path, pattern);
}

static string ExtractTitle(string text, string relPath)
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

static float ResolveWeight(string relPath, int fileSizeBytes, RankingSection ranking)
{
    var p = relPath.Replace('\\', '/');
    if (fileSizeBytes < ranking.StubByteThreshold && p.Contains("/example-implementation/"))
        return 0.05f;
    foreach (var entry in ranking.Weights)
        if (GlobMatch(p, entry.Pattern))
            return entry.Weight;
    return 1.0f;
}

static void WriteStatsMd(RagConfig cfg, ManifestService manifest, string repoRoot)
{
    var statsRel = cfg.Storage.StatsPath;
    if (string.IsNullOrWhiteSpace(statsRel)) return;

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
    sb.AppendLine($"Model: `{cfg.Embedder.Model}`  ");
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
