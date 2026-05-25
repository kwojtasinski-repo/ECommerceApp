using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace RagTools.Core.Ingest;

/// <summary>
/// Mechanical ZIP orchestrator. Streams the request body to a temp file (never holds the
/// whole archive in memory), opens it as a seekable <see cref="ZipArchive"/>, and delegates
/// every rule to <see cref="BatchValidator"/>. This class owns I/O and nothing else —
/// no policy, no validation.
///
/// Memory footprint at peak: 4KB body chunk during copy, plus one entry's content (typically
/// &lt;100KB per markdown doc) while it is being read into a <see cref="BatchDocument"/>.
/// </summary>
public sealed class ZipBatchParser(BatchValidator validator, ILogger<ZipBatchParser> logger) : IZipBatchParser
{
    public async Task<ZipParseOutcome> ParseAsync(Stream zipStream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(zipStream);

        // ── Stream body to a temp file (DeleteOnClose → auto-cleanup on dispose) ──
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"ragzip-{Guid.NewGuid():N}.zip");

        await using var tempFs = new FileStream(
            tempPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 4096,
            options: FileOptions.DeleteOnClose | FileOptions.Asynchronous);

        await zipStream.CopyToAsync(tempFs, bufferSize: 4096, ct).ConfigureAwait(false);

        if (tempFs.Length == 0)
        {
            return new ZipParseOutcome.Failure(
                BatchIngestError.EmptyBody,
                "Request body is empty.");
        }

        tempFs.Position = 0;

        // ── Open archive ──────────────────────────────────────────────────────
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(tempFs, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException ex)
        {
            return new ZipParseOutcome.Failure(
                BatchIngestError.InvalidZipArchive,
                "Invalid ZIP archive.",
                new Dictionary<string, object?> { ["reason"] = ex.Message });
        }

        using (zip)
        {
            // ── Collect entry descriptors (no streams, no content) ────────────
            var entries = zip.Entries
                .Where(e => !e.FullName.EndsWith('/'))
                .Select(e => new ZipEntryInfo(e.FullName, e.Length))
                .ToList();

            // ── Hand everything to the validator ──────────────────────────────
            var outcome = validator.Validate(entries, name =>
            {
                var entry = zip.GetEntry(name);
                if (entry is null) return null;
                using var s = entry.Open();
                using var r = new StreamReader(s);
                return r.ReadToEnd();
            });

            if (outcome is ValidationOutcome.Bad bad)
            {
                logger.LogInformation("ZIP rejected: {Error} — {Message}",
                    bad.Failure.Error, bad.Failure.Message);
                return bad.Failure;
            }

            var ok = (ValidationOutcome.Ok)outcome;

            // ── Read eligible doc contents into BatchDocuments ────────────────
            var documents = new List<BatchDocument>(ok.EligibleDocs.Count);
            foreach (var info in ok.EligibleDocs)
            {
                ct.ThrowIfCancellationRequested();

                var entry = zip.GetEntry(info.Name)
                            ?? zip.Entries.First(e => e.FullName.Replace('\\', '/') == info.Name);

                string content;
                await using (var stream = entry.Open())
                using (var reader = new StreamReader(stream))
                {
                    content = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
                }

                documents.Add(new BatchDocument(info.Name, content));
            }

            return new ZipParseOutcome.Success(new ParsedBatch(documents, ok.Rules, ok.Warnings));
        }
    }
}
