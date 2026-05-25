using Microsoft.Extensions.Logging;
using RagTools.Core.Primitives;

namespace RagTools.Core;

/// <summary>
/// CLI ingest loop: reads files from disk, delegates the chunk → embed → upsert
/// pipeline to <see cref="IDocumentProcessor"/>, then updates the manifest.
///
/// All pipeline logic (sanitize, title, kind, weight, chunk, embed, upsert) lives
/// in <see cref="DocumentProcessor"/> — single source of truth shared with the HTTP
/// path (<see cref="IngestWorker"/>).
/// </summary>
public sealed class FileIngestor
{
    private readonly IDocumentProcessor _processor;
    private readonly RagConfig _cfg;
    private readonly ManifestService _manifest;
    private readonly ILogger _log;

    public FileIngestor(
        IDocumentProcessor processor,
        RagConfig cfg,
        ManifestService manifest,
        ILogger log)
    {
        _processor = processor;
        _cfg = cfg;
        _manifest = manifest;
        _log = log;
    }

    /// <summary>
    /// Processes a list of files: reads, hands off to <see cref="IDocumentProcessor"/>,
    /// then updates the manifest. Returns the total chunk count.
    /// </summary>
    public async Task<int> IngestAsync(
        IReadOnlyList<(FileInfo File, string RelPath, string Hash)> toProcess,
        CancellationToken ct = default)
    {
        var collection = CollectionName.Parse(_cfg.Collection);
        var totalChunks = 0;
        var processedFiles = 0;

        foreach (var (fi, relPath, hash) in toProcess)
        {
            ct.ThrowIfCancellationRequested();

            var text = await File.ReadAllTextAsync(fi.FullName, ct);

            var result = await _processor.ProcessAsync(new DocumentProcessingRequest(
                Collection:       collection,
                RelPath:          relPath,
                Content:          text,
                FileSizeBytes:    (int)fi.Length,
                EnsureCollection: false,  // CLI ensures upfront in Program.cs
                StoreFullContent: false), // CLI path does not store full content
                ct);

            _manifest.Update(relPath, hash, result.ChunkCount);
            totalChunks += result.ChunkCount;
            processedFiles++;

            if (processedFiles % 10 == 0)
            {
                _log.LogInformation("{Done}/{Total} files processed ...", processedFiles, toProcess.Count);
            }
        }

        return totalChunks;
    }

    // ── Static helpers ────────────────────────────────────────────────────────
    // Preserved as thin delegates so external callers (e.g. Program.cs `IsExcluded`,
    // FileIngestorTests static-method coverage) keep working with single sources of truth.

    internal static string ExtractTitle(string text, string relPath)
        => DocumentMetadata.ExtractTitle(text, relPath);

    internal static string SanitizeText(string text, string relPath, ILogger logger)
        => TextSanitizer.RemoveReplacementChars(text, relPath, logger);

    internal static string SanitizeText(string text)
        => TextSanitizer.RemoveReplacementChars(text);

    internal static float ResolveWeight(string relPath, int fileSizeBytes, RankingSection ranking)
        => RankingWeightResolver.Resolve(relPath, fileSizeBytes, ranking);

    public static bool GlobMatch(string path, string glob)
        => RankingWeightResolver.GlobMatch(path, glob);
}

