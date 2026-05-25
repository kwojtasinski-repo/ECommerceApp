using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace RagTools.Core;

/// <summary>
/// Pushes markdown files to a remote mcp_server ingest endpoint via HTTP.
/// Mirrors the Python <c>remote_ingest_client.push_files_to_remote_server()</c>.
/// </summary>
public sealed class RemoteIngestClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger _log;

    public RemoteIngestClient(string baseUrl, string? apiKey, ILogger log)
    {
        _log = log;
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
        };
        if (!string.IsNullOrEmpty(apiKey))
            _http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    /// <summary>
    /// POST each file to <c>POST /ingest/{collection}</c>.
    /// Retries once on HTTP 503 (queue full).
    /// Updates the manifest on success.
    /// Returns the number of failed files.
    /// </summary>
    public async Task<int> PushAsync(
        string collection,
        IReadOnlyList<(FileInfo File, string RelPath, string Hash)> toProcess,
        ManifestService manifest,
        ILogger? dbg = null,
        CancellationToken ct = default)
    {
        dbg ??= _log;
        var processed = 0;
        var failed = 0;

        foreach (var (fi, relPath, hash) in toProcess)
        {
            ct.ThrowIfCancellationRequested();

            var content = await File.ReadAllTextAsync(fi.FullName, ct);
            var payload = new
            {
                rel_path = relPath,
                content,
                doc_kind = (string?)null,  // auto-detect on server
            };

            try
            {
                var response = await _http.PostAsJsonAsync(
                    $"ingest/{Uri.EscapeDataString(collection)}", payload, ct);

                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    _log.LogWarning("Server queue full for {RelPath}, retrying in 2s ...", relPath);
                    await Task.Delay(2000, ct);
                    response = await _http.PostAsJsonAsync(
                        $"ingest/{Uri.EscapeDataString(collection)}", payload, ct);
                }

                response.EnsureSuccessStatusCode();
                manifest.Update(relPath, hash, 0);  // chunk count unknown in remote mode
                processed++;
                dbg.LogDebug("queued {RelPath}", relPath);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "failed to upload {RelPath}", relPath);
                failed++;
            }
        }

        _log.LogInformation("remote ingest: {Processed} queued, {Failed} failed", processed, failed);
        return failed;
    }

    public void Dispose() => _http.Dispose();
}
