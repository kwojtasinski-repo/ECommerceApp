using Microsoft.Extensions.Logging;

namespace RagTools.Core;

/// <summary>
/// Orchestrates the chunk → embed → upsert pipeline for a batch of markdown files.
/// Extracted from Program.cs so the ingest loop is independently testable.
/// </summary>
public sealed class FileIngestor
{
    private readonly IEmbedder _embedder;
    private readonly QdrantStore _store;
    private readonly MarkdownChunker _chunker;
    private readonly RagConfig _cfg;
    private readonly ManifestService _manifest;
    private readonly ILogger _log;
    private const int BatchSize = 32;

    public FileIngestor(
        IEmbedder embedder,
        QdrantStore store,
        MarkdownChunker chunker,
        RagConfig cfg,
        ManifestService manifest,
        ILogger log)
    {
        _embedder = embedder;
        _store = store;
        _chunker = chunker;
        _cfg = cfg;
        _manifest = manifest;
        _log = log;
    }

    /// <summary>
    /// Processes a list of files: chunks, embeds, and upserts them into Qdrant.
    /// Returns the total number of chunks written.
    /// </summary>
    public async Task<int> IngestAsync(
        IReadOnlyList<(FileInfo File, string RelPath, string Hash)> toProcess,
        ILogger? dbg = null,
        CancellationToken ct = default)
    {
        dbg ??= _log;
        var repoRoot = _cfg.Workspace;
        var totalChunks = 0;
        var processedFiles = 0;

        foreach (var (fi, relPath, hash) in toProcess)
        {
            ct.ThrowIfCancellationRequested();

            var text = await File.ReadAllTextAsync(fi.FullName, ct);
            var chunks = _chunker.Chunk(text, relPath);
            var docTitle = ExtractTitle(text, relPath);
            var weight = ResolveWeight(relPath, (int)fi.Length, _cfg.Ranking);
            var kind = _cfg.DetectDocKind(relPath);
            var adrId = _cfg.DetectAdrId(relPath);

            // Remove any stale points for this file before re-upserting.
            await _store.DeleteByPathsAsync([relPath]);

            var points = new List<RagPoint>();
            for (var i = 0; i < chunks.Count; i += BatchSize)
            {
                var batch = chunks.Skip(i).Take(BatchSize).ToList();
                var texts = batch.Select(c => c.Breadcrumb + "\n\n" + c.Text).ToList();
                var vectors = await _embedder.EmbedBatchAsync(texts, ct);

                for (var j = 0; j < batch.Count; j++)
                {
                    var chunk = batch[j];
                    var chunkIndex = i + j;
                    var id = ManifestService.StableId(relPath, chunk.Breadcrumb, chunk.StartLine);
                    var contentId = DeterministicId.ForContent(_cfg.Collection, relPath);
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

            await _store.UpsertAsync(points);
            _manifest.Update(relPath, hash, chunks.Count);
            totalChunks += chunks.Count;
            processedFiles++;

            dbg.LogDebug("{RelPath}: {ChunkCount} chunks, kind={Kind}, weight={Weight}",
                relPath, chunks.Count, kind, weight);

            if (processedFiles % 10 == 0)
                _log.LogInformation("{Done}/{Total} files processed ...", processedFiles, toProcess.Count);
        }

        return totalChunks;
    }

    // ── Static helpers (mirrors Program.cs) ──────────────────────────────────

    internal static string ExtractTitle(string text, string relPath)
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

    internal static float ResolveWeight(string relPath, int fileSizeBytes, RankingSection ranking)
    {
        var p = relPath.Replace('\\', '/');
        if (fileSizeBytes < ranking.StubByteThreshold && p.Contains("/example-implementation/"))
            return 0.05f;
        foreach (var entry in ranking.Weights)
            if (GlobMatch(p, entry.Pattern))
                return entry.Weight;
        return 1.0f;
    }

    public static bool GlobMatch(string path, string glob)
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
}
