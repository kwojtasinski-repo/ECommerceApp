using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace RagTools.Core;

/// <summary>
/// Pushes markdown files to a remote mcp_server ingest endpoint via HTTP.
///
/// Wire protocol:
///   1. Build a single ZIP containing rag-config.yaml + metadata-rules.yaml + queries.yaml
///      at the ZIP root, plus every file-to-process at its workspace-relative path.
///   2. POST /ingest/{collection}/batch with Content-Type application/zip.
///   3. Parse batch_id + operations[] from the 202 response.
///   4. Poll GET /ingest/{collection}/operations/{operation_id} until every
///      operation is Completed or Failed.
///
/// Mirrors the Python remote_ingest_client.push_files_to_remote_server().
/// </summary>
public sealed class RemoteIngestClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger _log;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public RemoteIngestClient(string baseUrl, string? apiKey, ILogger log)
    {
        _log = log;
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromMinutes(5),
        };
        if (!string.IsNullOrEmpty(apiKey))
            _http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    // Server queue capacity is 100; stay safely below.
    private const int BatchChunkSize = 80;

    public async Task<int> PushAsync(
        RagConfig cfg,
        IReadOnlyList<(FileInfo File, string RelPath, string Hash)> toProcess,
        ManifestService manifest,
        CancellationToken ct = default)
    {
        if (toProcess.Count == 0)
        {
            _log.LogInformation("remote push: no files to send");
            return 0;
        }

        if (toProcess.Count <= BatchChunkSize)
            return await PushOneBatchAsync(cfg, toProcess, manifest, "1/1", ct);

        var chunks = new List<IReadOnlyList<(FileInfo, string, string)>>();
        for (var i = 0; i < toProcess.Count; i += BatchChunkSize)
        {
            var take = Math.Min(BatchChunkSize, toProcess.Count - i);
            var slice = new (FileInfo, string, string)[take];
            for (var j = 0; j < take; j++) slice[j] = toProcess[i + j];
            chunks.Add(slice);
        }
        _log.LogInformation("splitting {Total} file(s) into {N} batch(es) of ≤{Size}",
            toProcess.Count, chunks.Count, BatchChunkSize);

        var totalFailed = 0;
        for (var i = 0; i < chunks.Count; i++)
        {
            var failed = await PushOneBatchAsync(
                cfg, chunks[i], manifest, $"{i + 1}/{chunks.Count}", ct);
            totalFailed += failed;
        }
        _log.LogInformation("remote ingest: {Ok} ok, {Failed} failed ({Total} total)",
            toProcess.Count - totalFailed, totalFailed, toProcess.Count);
        return totalFailed;
    }

    private async Task<int> PushOneBatchAsync(
        RagConfig cfg,
        IReadOnlyList<(FileInfo File, string RelPath, string Hash)> toProcess,
        ManifestService manifest,
        string label,
        CancellationToken ct)
    {
        var collection = cfg.Collection;
        byte[] zipBytes;
        try
        {
            zipBytes = BuildBatchZip(cfg, toProcess);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "failed to build batch ZIP");
            return toProcess.Count;
        }

        _log.LogInformation(
            "batch {Label} ZIP ready ({Bytes} bytes), POST /ingest/{Collection}/batch ({Count} file(s))",
            label, zipBytes.Length, collection, toProcess.Count);

        BatchAcceptedResponse? batch = null;
        const int MaxAttempts = 8; // ~2 min of 503 retries (server queue drain)
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            using var content = new ByteArrayContent(zipBytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
            try
            {
                using var resp = await _http.PostAsync(
                    $"ingest/{Uri.EscapeDataString(collection)}/batch", content, ct);

                if (resp.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable && attempt < MaxAttempts - 1)
                {
                    var wait = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                    _log.LogWarning("batch POST 503 queue full — retry in {Wait}s", wait.TotalSeconds);
                    await Task.Delay(wait, ct);
                    continue;
                }

                if (!resp.IsSuccessStatusCode)
                {
                    var errBody = await resp.Content.ReadAsStringAsync(ct);
                    _log.LogError("batch POST failed HTTP {Code} — {Body}",
                        (int)resp.StatusCode, errBody.Length > 400 ? errBody[..400] : errBody);
                    return toProcess.Count;
                }

                var json = await resp.Content.ReadAsStringAsync(ct);
                batch = JsonSerializer.Deserialize<BatchAcceptedResponse>(json, JsonOpts);
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "batch POST exception (attempt {N})", attempt);
                if (attempt >= MaxAttempts - 1) return toProcess.Count;
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }

        if (batch?.Operations is null || batch.Operations.Count == 0)
        {
            _log.LogError("server returned no operations");
            return toProcess.Count;
        }

        var relByOp = batch.Operations
            .Where(o => !string.IsNullOrEmpty(o.OperationId))
            .ToDictionary(o => o.OperationId!, o => o.RelPath ?? "?");
        _log.LogInformation("batch accepted — {Count} operation(s), polling …", relByOp.Count);

        var hashByRel = toProcess.ToDictionary(t => t.RelPath, t => t.Hash);

        var remaining = new HashSet<string>(relByOp.Keys);
        var failed = 0;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(180, remaining.Count * 5));

        while (remaining.Count > 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
            foreach (var opId in remaining.ToList())
            {
                OperationStatusResponse? op = null;
                try
                {
                    using var resp = await _http.GetAsync(
                        $"ingest/{Uri.EscapeDataString(collection)}/operations/{Uri.EscapeDataString(opId)}", ct);
                    if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _log.LogError("FAIL {Rel}: operation 404", relByOp[opId]);
                        failed++;
                        remaining.Remove(opId);
                        continue;
                    }
                    if (!resp.IsSuccessStatusCode) continue;
                    var json = await resp.Content.ReadAsStringAsync(ct);
                    op = JsonSerializer.Deserialize<OperationStatusResponse>(json, JsonOpts);
                }
                catch { continue; }

                var status = op?.Status?.Trim() ?? "";
                if (status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
                    status.Equals("Complete", StringComparison.OrdinalIgnoreCase))
                {
                    var rel = relByOp[opId];
                    if (hashByRel.TryGetValue(rel, out var hash))
                        manifest.Update(rel, hash, op?.ChunkCount ?? 0);
                    _log.LogInformation("OK  {Rel} → {Chunks} chunk(s)", rel, op?.ChunkCount ?? 0);
                    remaining.Remove(opId);
                }
                else if (status.Equals("Failed", StringComparison.OrdinalIgnoreCase))
                {
                    _log.LogError("FAIL {Rel}: {Err}", relByOp[opId],
                        op?.ErrorMessage ?? "unknown error");
                    failed++;
                    remaining.Remove(opId);
                }
            }
        }

        foreach (var opId in remaining)
        {
            _log.LogError("TIMEOUT {Rel}: operation did not complete in time", relByOp[opId]);
            failed++;
        }

        var ok = relByOp.Count - failed;
        _log.LogInformation("remote ingest: {Ok} ok, {Failed} failed ({Total} total)",
            ok, failed, relByOp.Count);
        return failed;
    }

    private static byte[] BuildBatchZip(
        RagConfig cfg,
        IReadOnlyList<(FileInfo File, string RelPath, string Hash)> files)
    {
        if (string.IsNullOrEmpty(cfg.LoadedFrom))
            throw new InvalidOperationException("RagConfig.LoadedFrom is required for remote batch upload");
        if (string.IsNullOrEmpty(cfg.MetadataRulesPath) || !File.Exists(cfg.MetadataRulesPath))
            throw new InvalidOperationException(
                "metadata-rules.yaml not found — required for batch upload");
        if (string.IsNullOrEmpty(cfg.QueriesPath) || !File.Exists(cfg.QueriesPath))
            throw new InvalidOperationException(
                "queries.yaml not found — required for batch upload");

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(zip, "rag-config.yaml", File.ReadAllBytes(cfg.LoadedFrom));
            AddEntry(zip, "metadata-rules.yaml", File.ReadAllBytes(cfg.MetadataRulesPath));
            AddEntry(zip, "queries.yaml", File.ReadAllBytes(cfg.QueriesPath));
            if (!string.IsNullOrEmpty(cfg.GlossaryPath) && File.Exists(cfg.GlossaryPath))
                AddEntry(zip, "multilingual-glossary.yaml", File.ReadAllBytes(cfg.GlossaryPath));
            foreach (var (fi, rel, _) in files)
            {
                try { AddEntry(zip, rel.Replace('\\', '/'), File.ReadAllBytes(fi.FullName)); }
                catch (IOException) { /* skip unreadable */ }
            }
        }
        return ms.ToArray();
    }

    private static void AddEntry(ZipArchive zip, string entryName, byte[] data)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var es = entry.Open();
        es.Write(data, 0, data.Length);
    }

    public void Dispose() => _http.Dispose();

    private sealed class BatchAcceptedResponse
    {
        [JsonPropertyName("batch_id")] public string? BatchId { get; set; }
        [JsonPropertyName("count")] public int Count { get; set; }
        [JsonPropertyName("operations")] public List<BatchOperationStub>? Operations { get; set; }
    }

    private sealed class BatchOperationStub
    {
        [JsonPropertyName("operation_id")] public string? OperationId { get; set; }
        [JsonPropertyName("rel_path")] public string? RelPath { get; set; }
    }

    private sealed class OperationStatusResponse
    {
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("chunk_count")] public int? ChunkCount { get; set; }
        [JsonPropertyName("error_message")] public string? ErrorMessage { get; set; }
    }
}
