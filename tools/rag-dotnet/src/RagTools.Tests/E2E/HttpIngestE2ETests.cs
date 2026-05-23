using System.IO.Compression;
using System.Net;
using System.Text.Json;
using RagTools.Core;
using Xunit;
using Xunit.Abstractions;

namespace RagTools.Tests.E2E;

/// <summary>
/// Black-box HTTP integration tests for the ingest upload API.
///
/// These tests exercise the full HTTP path end-to-end:
///
///   POST /ingest/{collection}/batch
///     → ApiKeyMiddleware (dev-mode pass-through when RAG_API_KEY unset)
///     → IngestController (validates ZIP, enqueues, returns 202)
///     → IngestChannel
///     → IngestWorker (embeds, upserts to Qdrant)
///     → OperationStore (status transitions)
///
///   GET /ingest/{collection}/operations/{opId}
///     → OperationStore read
///
///   GET /ingest/{collection}/operations
///     → list all for collection
///
///   GET /admin/stats
///     → queue depth + retention info
///
/// All tests require the ONNX model and Qdrant (Docker or QDRANT_URL env var).
/// </summary>
[Trait("Category", "E2E")]
[Collection(RagTestCollection.Name)]
public sealed class HttpIngestE2ETests : IClassFixture<HttpIngestE2EFixture>, IDisposable
{
    private readonly HttpIngestE2EFixture _fx;
    private readonly SharedOnnxFixture    _sharedOnnx;

    public HttpIngestE2ETests(
        HttpIngestE2EFixture fixture,
        SharedOnnxFixture sharedOnnx,
        ITestOutputHelper output)
    {
        _fx         = fixture;
        _sharedOnnx = sharedOnnx;
        _fx.Sink.SetOutput(output);
        sharedOnnx.Sink.SetOutput(output);
    }

    public void Dispose()
    {
        _fx.Sink.SetOutput(null);
        _sharedOnnx.Sink.SetOutput(null);
    }

    private static byte[] BuildZip(string relPath, string content)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry(relPath);
            using var writer = new System.IO.StreamWriter(entry.Open(), System.Text.Encoding.UTF8);
            writer.Write(content);
        }
        return ms.ToArray();
    }

    private HttpContent ZipContent(string relPath, string content)
    {
        var bytes = BuildZip(relPath, content);
        var sc = new ByteArrayContent(bytes);
        sc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        return sc;
    }

    // ── POST /ingest/{collection}/batch — happy path ──────────────────────────

    [Fact]
    public async Task UploadBatch_Returns202_WithOperationsArray()
    {
        using var resp = await _fx.Client!.PostAsync(
            $"/ingest/{_fx.Collection}/batch",
            ZipContent("docs/concepts/solid.md", "# SOLID\n\nFive design principles."));

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);

        var body = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("operations", out var ops));
        Assert.True(ops.GetArrayLength() > 0);
        var first = ops.EnumerateArray().First();
        Assert.True(first.TryGetProperty("operationId", out var opIdEl));
        Assert.NotEmpty(opIdEl.GetString()!);
        Assert.True(first.TryGetProperty("statusUrl", out var urlEl));
        Assert.Contains($"/ingest/{_fx.Collection}/operations/", urlEl.GetString());
    }

    [Fact]
    public async Task UploadBatch_Returns400_WhenBodyIsNotZip()
    {
        var sc = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("not a zip"));
        sc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        using var resp = await _fx.Client!.PostAsync($"/ingest/{_fx.Collection}/batch", sc);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task UploadBatch_Returns400_WhenZipIsEmpty()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true)) { /* empty */ }
        var bytes = ms.ToArray();
        var sc = new ByteArrayContent(bytes);
        sc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        using var resp = await _fx.Client!.PostAsync($"/ingest/{_fx.Collection}/batch", sc);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── Full batch→index→verify pipeline ─────────────────────────────────────

    [Fact]
    public async Task UploadBatch_FullPipeline_ContentIsIndexedInQdrant()
    {
        const string relPath = "docs/concepts/hexagonal.md";
        const string content = """
            # Hexagonal Architecture

            Also known as Ports and Adapters.
            The application core is isolated from infrastructure concerns.
            Adapters implement ports defined by the domain.
            """;

        var finalStatus = await _fx.UploadAndWaitAsync(_fx.Collection!, relPath, content);

        Assert.NotNull(finalStatus);
        Assert.Equal("Completed", finalStatus!.RootElement.GetProperty("status").GetString());

        var doc = await _fx.Store!.FetchContentAsync(_fx.Collection!, relPath);
        Assert.NotNull(doc);
        Assert.Contains("Hexagonal Architecture", doc!.Content);
        Assert.Equal(relPath, doc.RelPath);
    }

    [Fact]
    public async Task UploadBatch_FullPipeline_ChunkCountIsPositive()
    {
        const string relPath = "docs/concepts/cqrs.md";
        const string content = """
            # CQRS

            Command Query Responsibility Segregation separates reads and writes.
            Commands change state. Queries read state without side effects.
            Both sides can be scaled independently.
            """;

        var finalStatus = await _fx.UploadAndWaitAsync(_fx.Collection!, relPath, content);

        Assert.NotNull(finalStatus);
        Assert.Equal("Completed", finalStatus!.RootElement.GetProperty("status").GetString());
        var chunkCount = finalStatus.RootElement.GetProperty("chunkCount").GetInt32();
        Assert.True(chunkCount > 0, $"Expected at least one chunk, got {chunkCount}");
    }

    // ── GET /ingest/{collection}/operations/{opId} ────────────────────────────

    [Fact]
    public async Task PollStatus_TransitionsFromQueued_ToCompleted()
    {
        using var postResp = await _fx.Client!.PostAsync(
            $"/ingest/{_fx.Collection}/batch",
            ZipContent("docs/concepts/ddd.md", "# Domain-Driven Design\n\nModel your domain, not your database."));

        Assert.Equal(HttpStatusCode.Accepted, postResp.StatusCode);

        var postBody  = JsonDocument.Parse(await postResp.Content.ReadAsStringAsync());
        var firstOp   = postBody.RootElement.GetProperty("operations").EnumerateArray().First();
        var statusUrl = firstOp.GetProperty("statusUrl").GetString()!;

        // Immediately after upload the status should be Queued or Processing.
        using var immediateResp = await _fx.Client.GetAsync(statusUrl);
        Assert.Equal(HttpStatusCode.OK, immediateResp.StatusCode);
        var immediateDoc    = JsonDocument.Parse(await immediateResp.Content.ReadAsStringAsync());
        var immediateStatus = immediateDoc.RootElement.GetProperty("status").GetString();
        Assert.True(
            immediateStatus is "Queued" or "Processing",
            $"Expected Queued or Processing immediately after upload, got {immediateStatus}");

        // Wait for completion.
        await _fx.UploadAndWaitAsync(
            _fx.Collection!,
            "docs/concepts/ddd-warm-up.md",
            "# DDD Warm-up\n\nBounded context keeps model coherent.");

        // Poll the original operation until it completes.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        string? finalStatus = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var pollResp = await _fx.Client.GetAsync(statusUrl);
            var pollDoc = JsonDocument.Parse(await pollResp.Content.ReadAsStringAsync());
            finalStatus = pollDoc.RootElement.GetProperty("status").GetString();
            if (finalStatus is "Completed" or "Failed") break;
            await Task.Delay(300);
        }

        Assert.Equal("Completed", finalStatus);
    }

    [Fact]
    public async Task PollStatus_Returns404_ForUnknownOperationId()
    {
        using var resp = await _fx.Client!.GetAsync(
            $"/ingest/{_fx.Collection}/operations/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task PollStatus_Returns404_ForWrongCollection()
    {
        using var postResp = await _fx.Client!.PostAsync(
            $"/ingest/{_fx.Collection}/batch",
            ZipContent("docs/isolation/test.md", "# Isolation Test\n\nCollection boundary must be enforced."));

        Assert.Equal(HttpStatusCode.Accepted, postResp.StatusCode);

        var postBody = JsonDocument.Parse(await postResp.Content.ReadAsStringAsync());
        var firstOp  = postBody.RootElement.GetProperty("operations").EnumerateArray().First();
        var opId     = firstOp.GetProperty("operationId").GetString()!;

        // Look up with a DIFFERENT collection name — must return 404.
        using var getResp = await _fx.Client.GetAsync(
            $"/ingest/wrong-collection/operations/{Uri.EscapeDataString(opId)}");
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    // ── GET /ingest/{collection}/operations (list) ────────────────────────────

    [Fact]
    public async Task ListOperations_ReturnsUploadedOperations()
    {
        // Upload two documents via batch.
        await _fx.UploadAndWaitAsync(_fx.Collection!, "docs/list/a.md", "# Doc A\n\nContent A.");
        await _fx.UploadAndWaitAsync(_fx.Collection!, "docs/list/b.md", "# Doc B\n\nContent B.");

        using var resp = await _fx.Client!.GetAsync($"/ingest/{_fx.Collection}/operations");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var list  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var items = list.RootElement.EnumerateArray().ToList();

        Assert.True(items.Count >= 2, $"Expected >= 2 operations, got {items.Count}");
        Assert.All(items, item =>
            Assert.Equal(_fx.Collection, item.GetProperty("collection").GetString()));
    }

    // ── GET /admin/stats ──────────────────────────────────────────────────────

    [Fact]
    public async Task AdminStats_Returns200_WithQueueDepthAndRetention()
    {
        using var resp = await _fx.Client!.GetAsync("/admin/stats");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("queue_depth", out _),
            "stats must include 'queue_depth'");
        Assert.True(doc.RootElement.TryGetProperty("retention_hours", out _),
            "stats must include 'retention_hours'");
    }

    // ── Re-upload idempotency ─────────────────────────────────────────────────

    [Fact]
    public async Task ReUpload_ReplacesContent_WithUpdatedVersion()
    {
        const string relPath = "docs/concepts/retry.md";
        const string v1 = "# Retry\n\nExponential back-off avoids thundering herd.";
        const string v2 = "# Retry\n\nExponential back-off avoids thundering herd. Circuit breaker prevents cascading failures.";

        await _fx.UploadAndWaitAsync(_fx.Collection!, relPath, v1);
        await _fx.UploadAndWaitAsync(_fx.Collection!, relPath, v2);

        var doc = await _fx.Store!.FetchContentAsync(_fx.Collection!, relPath);
        Assert.NotNull(doc);
        Assert.Contains("Circuit breaker", doc!.Content);
    }
}
