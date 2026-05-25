using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using RagTools.Core;

namespace RagTools.Mcp.Controllers;

/// <summary>
/// Ingest controller — accepts document uploads and queues them for async processing.
///
/// Endpoints:
///   POST   /ingest/{collection}/batch                     — upload a ZIP of documents
///   GET    /ingest/{collection}/operations/{opId}         — poll operation status
///   GET    /ingest/{collection}/operations                — list all operations for collection
///   GET    /admin/stats                                   — queue depth + worker health
///
/// Authentication: all /ingest/* routes require X-Api-Key header (ApiKeyMiddleware).
/// </summary>
[ApiController]
public sealed class IngestController(
    IngestChannel channel,
    OperationStore operations,
    ILogger<IngestController> logger) : ControllerBase
{
    private const long MaxBodyBytes = 50L * 1024 * 1024; // 50 MB
    private static readonly Regex CollectionNameRe =
        new(@"^[a-z0-9][a-z0-9_-]*$", RegexOptions.Compiled);
    private static readonly HashSet<string> ConfigFiles =
        new(StringComparer.OrdinalIgnoreCase) { "metadata-rules.yaml", "queries.yaml", "multilingual-glossary.yaml" };

    /// <summary>Poll the status of an ingest operation.</summary>
    [HttpGet("/ingest/{collection}/operations/{opId}")]
    public IActionResult GetOperation(string collection, string opId)
    {
        var op = operations.Get(opId);
        if (op is null || op.Collection != collection)
            return NotFound(new { error = $"Operation {opId} not found in collection {collection}" });

        return Ok(op);
    }

    /// <summary>List all recent operations for a collection.</summary>
    [HttpGet("/ingest/{collection}/operations")]
    public IActionResult ListOperations(string collection) =>
        Ok(operations.GetByCollection(collection));

    /// <summary>Queue depth and basic worker health.</summary>
    [HttpGet("/admin/stats")]
    public IActionResult Stats() => Ok(new
    {
        queue_depth    = channel.PendingCount,
        retention_hours = OperationStore.RetentionPeriod.TotalHours,
    });

    /// <summary>
    /// Upload a ZIP archive of documents for async ingestion.
    ///
    /// Body: raw <c>application/zip</c> bytes — each file in the archive is ingested as a separate job.
    /// Returns: 202 Accepted with a <see cref="BatchIngestResponse"/> listing all queued operations.
    /// Returns: 400 Bad Request when the body is not a valid ZIP or contains no .md files.
    /// Returns: 413 Payload Too Large when the body exceeds 50 MB.
    /// Returns: 415 Unsupported Media Type when Content-Type is not application/zip.
    /// Returns: 503 Service Unavailable when the ingest queue cannot fit all files.
    /// </summary>
    [HttpPost("/ingest/{collection}/batch")]
    [RequestSizeLimit(52_428_800)] // 50 MB — enforced before body is buffered
    public async Task<IActionResult> UploadBatch(string collection)
    {
        // ── Collection name sanitization ──────────────────────────────────────
        if (!CollectionNameRe.IsMatch(collection))
            return BadRequest(new { error = $"Invalid collection name '{collection}'. Must match [a-z0-9][a-z0-9_-]*." });

        // ── Content-Type validation ───────────────────────────────────────────
        var ct = Request.ContentType ?? string.Empty;
        if (!ct.StartsWith("application/zip", StringComparison.OrdinalIgnoreCase) &&
            !ct.StartsWith("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return StatusCode(415, new { error = $"Expected Content-Type application/zip, got '{ct}'" });

        // ── Body size pre-check (Content-Length) ──────────────────────────────
        if (Request.ContentLength > MaxBodyBytes)
            return StatusCode(413, new { error = $"Request body too large. Limit is {MaxBodyBytes / (1024 * 1024)} MB." });

        // ── Read body ─────────────────────────────────────────────────────────
        using var ms = new System.IO.MemoryStream();
        await Request.Body.CopyToAsync(ms);

        if (ms.Length == 0)
        {
            return BadRequest(new { error = "Request body is empty" });
        }
        if (ms.Length > MaxBodyBytes)
        {
            return StatusCode(413, new { error = $"Request body too large ({ms.Length:N0} bytes). Limit is {MaxBodyBytes / (1024 * 1024)} MB." });
        }

        ms.Position = 0;

        // ── Parse ZIP ─────────────────────────────────────────────────────────
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            return BadRequest(new { error = "Invalid ZIP archive" });
        }

        using (zip)
        {
            var warnings = new List<string>();
            var zipNames = zip.Entries.Select(e => e.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // ── Required config files ─────────────────────────────────────────
            foreach (var required in new[] { "metadata-rules.yaml", "queries.yaml" })
            {
                if (!zipNames.Contains(required))
                {
                    var yamlFiles = zipNames.Where(n => n.EndsWith(".yaml") || n.EndsWith(".yml")).ToList();
                    var hint = yamlFiles.Count > 0
                        ? $" Found YAML files: [{string.Join(", ", yamlFiles)}]."
                        : string.Empty;
                    return BadRequest(new { error = $"Required file '{required}' not found in ZIP root.{hint}" });
                }
            }

            string metaContent;
            using (var r = new System.IO.StreamReader(zip.GetEntry("metadata-rules.yaml")!.Open()))
                metaContent = await r.ReadToEndAsync();
            if (!Regex.IsMatch(metaContent, @"doc_kind_rules:\s*\r?\n\s+-"))
                return BadRequest(new { error = "metadata-rules.yaml must contain at least one doc_kind_rules entry" });

            // Parse the ZIP's metadata rules so the worker can detect adr_id / doc_kind per file
            // without needing the companion file to exist in the container's filesystem.
            MetadataRulesSection? batchRules = null;
            try { batchRules = RagConfig.ParseMetadataRules(metaContent); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to parse metadata-rules.yaml — adr_id/doc_kind will fall back to global config"); }

            string queriesContent;
            using (var r = new System.IO.StreamReader(zip.GetEntry("queries.yaml")!.Open()))
                queriesContent = await r.ReadToEndAsync();
            if (!Regex.IsMatch(queriesContent, @"named_queries:\s*\r?\n\s+-"))
                return BadRequest(new { error = "queries.yaml must contain at least one named_queries entry" });

            // Strip YAML comment lines before regex-scanning so that doc_kind: values
            // that appear only inside comments are not treated as real references.
            var metaNoComments     = StripYamlComments(metaContent);
            var queriesNoComments  = StripYamlComments(queriesContent);

            var knownKinds = Regex.Matches(metaNoComments, @"\bkind:\s*(\S+)")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var badKinds = Regex.Matches(queriesNoComments, @"\bdoc_kind:\s*(\S+)")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value.Trim())
                .Where(k => !knownKinds.Contains(k))
                .Distinct()
                .OrderBy(k => k)
                .ToList();
            if (badKinds.Count > 0)
                return BadRequest(new { error = $"queries.yaml references unknown doc_kind(s): [{string.Join(", ", badKinds)}]. Add matching rules to metadata-rules.yaml." });

            // ── multilingual-glossary.yaml ────────────────────────────────────
            if (!zipNames.Contains("multilingual-glossary.yaml"))
                warnings.Add("multilingual-glossary.yaml not found in ZIP — Polish/German query expansion will be reduced.");

            // ── Document entries ──────────────────────────────────────────────
            var fileEntries = new List<ZipArchiveEntry>();
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/')) continue;
                if (ConfigFiles.Contains(entry.FullName)) continue;

                // Path traversal protection
                var normalized = entry.FullName.Replace('\\', '/');
                if (normalized.Split('/').Any(p => p == ".."))
                    return BadRequest(new { error = $"Path traversal detected in ZIP entry '{entry.FullName}'" });

                // Extension check — only .md files accepted as documents
                if (!entry.FullName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"Skipped non-.md file: '{entry.FullName}'");
                    continue;
                }

                // Zero-byte filter
                if (entry.Length == 0)
                {
                    warnings.Add($"Skipped zero-byte file: '{entry.FullName}'");
                    continue;
                }

                fileEntries.Add(entry);
            }

            if (fileEntries.Count == 0)
                return BadRequest(new { error = "ZIP contains no .md document files" });

            if (channel.PendingCount + fileEntries.Count > channel.Capacity)
            {
                logger.LogWarning("Ingest queue full, rejecting batch of {Count} files for {Collection}", fileEntries.Count, collection);
                return StatusCode(503, new { error = "Ingest queue is full. Retry after a moment.", pending = channel.PendingCount });
            }

            var enqueuedAt = DateTimeOffset.UtcNow;
            var opList     = new List<BatchOperationEntry>(fileEntries.Count);

            foreach (var entry in fileEntries)
            {
                var relPath = entry.FullName.Replace('\\', '/');
                string content;
                using (var reader = new System.IO.StreamReader(entry.Open()))
                    content = await reader.ReadToEndAsync();

                var safeRelPath = relPath.Replace('/', '-');
                var opId = $"{collection}:{safeRelPath}:{enqueuedAt.Ticks}-{opList.Count}";

                var docKind = batchRules is not null ? RagConfig.DetectDocKindFromRules(relPath, batchRules) : null;
                var adrId   = batchRules is not null ? RagConfig.DetectAdrIdFromRules(relPath, batchRules)  : null;

                var job = new IngestJob
                {
                    OperationId = opId,
                    Collection  = collection,
                    RelPath     = relPath,
                    Content     = content,
                    DocKind     = docKind,
                    AdrId       = adrId,
                    EnqueuedAt  = enqueuedAt,
                };

                if (!channel.TryWrite(job))
                {
                    logger.LogError("Failed to enqueue job {OpId} — channel unexpectedly full", opId);
                    return StatusCode(503, new { error = "Ingest channel unexpectedly full. Retry after a moment." });
                }

                operations.MarkQueued(opId, collection, relPath, enqueuedAt);

                var statusUrl = $"/ingest/{collection}/operations/{Uri.EscapeDataString(opId)}";
                opList.Add(new BatchOperationEntry { RelPath = relPath, OperationId = opId, StatusUrl = statusUrl });
            }

            logger.LogInformation("Batch queued {Count} jobs for {Collection}", opList.Count, collection);

            return Accepted(new BatchIngestResponse
            {
                BatchId    = $"batch:{collection}:{enqueuedAt.Ticks}",
                Count      = opList.Count,
                Operations = opList,
                Warnings   = warnings.Count > 0 ? warnings : null,
            });
        }
    }

    /// <summary>
    /// Remove lines that are YAML comments (trimmed line starts with '#') so that
    /// regex scans over kind: / doc_kind: do not pick up values from comment text.
    /// </summary>
    private static string StripYamlComments(string yaml) =>
        string.Join('\n', yaml.Split('\n').Where(l => !l.TrimStart().StartsWith('#')));
}

/// <summary>Response body for 202 Accepted from POST /ingest/{collection}/batch.</summary>
public sealed class BatchIngestResponse
{
    public string                    BatchId    { get; set; } = string.Empty;
    public int                       Count      { get; set; }
    public List<BatchOperationEntry> Operations { get; set; } = [];
    /// <summary>Non-fatal warnings (skipped files, missing optional resources).</summary>
    public List<string>?             Warnings   { get; set; }
}

/// <summary>Per-file entry within <see cref="BatchIngestResponse"/>.</summary>
public sealed class BatchOperationEntry
{
    public string OperationId { get; set; } = string.Empty;
    public string RelPath     { get; set; } = string.Empty;
    public string StatusUrl   { get; set; } = string.Empty;
}
