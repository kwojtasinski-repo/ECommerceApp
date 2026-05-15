using RagTools.Core;

// ── CLI entry point for incremental ingest ────────────────────────────────────
//
// Usage:
//   dotnet run                 # incremental — only changed/new files
//   dotnet run -- --force-full # full rebuild, ignores manifest
//   dotnet run -- --dry-run    # print kind distribution without embedding
//
// Environment variables:
//   RAG_WORKSPACE    absolute path to the repo root (default: cwd)
//   RAG_COLLECTION   Qdrant collection name override (default: from config.yaml)
//   RAG_CONFIG       path to config.yaml (default: /app/config.yaml)
//   QDRANT_URL       Qdrant server URL (default: from config.yaml)

var configPath = Environment.GetEnvironmentVariable("RAG_CONFIG")
    ?? Path.Combine(AppContext.BaseDirectory, "config.yaml");

var cfg = RagConfig.Load(configPath);

var forceFull = args.Contains("--force-full");
var dryRun = args.Contains("--dry-run");

var repoRoot = RagConfig.RepoRoot;
var manifestPath = cfg.ManifestAbsPath;
var modelDir = Environment.GetEnvironmentVariable("RAG_MODEL_DIR")
    ?? Path.Combine(AppContext.BaseDirectory, "model");

Console.WriteLine($"[ingest] repo root  : {repoRoot}");
Console.WriteLine($"[ingest] collection : {cfg.Collection}");
Console.WriteLine($"[ingest] manifest   : {manifestPath}");
Console.WriteLine($"[ingest] model dir  : {modelDir}");
Console.WriteLine($"[ingest] mode       : {(forceFull ? "full" : "incremental")}");

// ── Collect markdown files ────────────────────────────────────────────────────
var sourceRoots = cfg.Source.Roots
    .Select(r => Path.Combine(repoRoot, r))
    .Where(Directory.Exists)
    .ToList();

if (sourceRoots.Count == 0)
{
    Console.Error.WriteLine("[ingest] ERROR: No source roots found. Check config.yaml source.roots.");
    return 1;
}

var allFiles = sourceRoots
    .SelectMany(root => Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
    .Select(f => new FileInfo(f))
    .Where(fi => !IsExcluded(fi, repoRoot, cfg.Source.ExcludeGlobs))
    .OrderBy(fi => fi.FullName)
    .ToList();

Console.WriteLine($"[ingest] found {allFiles.Count} markdown files");

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
    Console.WriteLine("\n[dry-run] document kind distribution:");
    foreach (var (kind, count) in kindCounts.OrderByDescending(kv => kv.Value))
        Console.WriteLine($"  {kind,-30} {count,5} files");
    return 0;
}

// ── Load manifest (incremental) ───────────────────────────────────────────────
var manifest = forceFull
    ? ManifestService.Load(string.Empty) // empty → no prior entries
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

Console.WriteLine($"[ingest] to process : {toProcess.Count} file(s) (changed/new)");
Console.WriteLine($"[ingest] to delete  : {deleted.Count} file(s) (removed from disk)");

if (toProcess.Count == 0 && deleted.Count == 0)
{
    Console.WriteLine("[ingest] nothing to do — index is up to date");
    return 0;
}

// ── Load embedder + Qdrant ────────────────────────────────────────────────────
Console.WriteLine("[ingest] loading ONNX embedder ...");
using var embedder = OnnxEmbedder.Load(modelDir);
Console.WriteLine($"[ingest] embedding dimensions: {embedder.Dimensions}");

var qdrantUrl = Environment.GetEnvironmentVariable("QDRANT_URL") ?? cfg.QdrantUrl;
using var store = QdrantStore.Connect(qdrantUrl, cfg.Collection);
await store.EnsureCollectionAsync(embedder.Dimensions);

// ── Delete removed files ──────────────────────────────────────────────────────
if (deleted.Count > 0)
{
    Console.WriteLine("[ingest] deleting stale points ...");
    await store.DeleteByPathsAsync(deleted);
    foreach (var rel in deleted)
        manifest.Remove(rel);
}

// ── Chunk, embed, upsert ──────────────────────────────────────────────────────
var tokenCounter = BertTokenCounter.FromModelDir(modelDir);
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
            var id = ManifestService.StableId(relPath, chunk.Breadcrumb, chunk.StartLine);
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
                Text: chunk.Text)));
        }
    }

    await store.UpsertAsync(points);
    manifest.Update(relPath, hash, chunks.Count);
    totalChunks += chunks.Count;
    processedFiles++;

    if (processedFiles % 10 == 0)
        Console.WriteLine($"[ingest] {processedFiles}/{toProcess.Count} files processed ...");
}

manifest.Save();
WriteStatsMd(cfg, manifest, repoRoot);
Console.WriteLine($"[ingest] done — {processedFiles} file(s), {totalChunks} chunks, manifest saved");
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
