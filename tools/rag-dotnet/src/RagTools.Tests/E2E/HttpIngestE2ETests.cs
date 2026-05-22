using System.Net;
using System.Text;
using System.Text.Json;
using RagTools.Core;
using Xunit;

namespace RagTools.Tests.E2E;

/// <summary>
/// Black-box HTTP integration tests for the ingest upload API.
///
/// These tests exercise the full HTTP path end-to-end:
///
///   POST /ingest/{collection}
///     → ApiKeyMiddleware (dev-mode pass-through when RAG_API_KEY unset)
///     → IngestController (validates, enqueues, returns 202)
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
/// They are automatically skipped when neither is available.
/// </summary>
[Trait("Category", "E2E")]
public sealed class HttpIngestE2ETests : IClassFixture<HttpIngestE2EFixture>
{
    private readonly HttpIngestE2EFixture _fx;

    public HttpIngestE2ETests(HttpIngestE2EFixture fixture) => _fx = fixture;

    // ── POST /ingest/{collection} — happy path ────────────────────────────────

    [SkippableFact]
    public async Task Upload_Returns202_WithOperationIdAndLocationHeader()
    {
        Skip.If(!_fx.IsAvailable, _fx.SkipReason);

        var payload = JsonSerializer.Serialize(new
        {
            relPath = "docs/concepts/solid.md",
            content = "# SOLID\n\nFive design principles: SRP, OCP, LSP, ISP, DIP.",
        });

        using var resp = await _fx.Client!.PostAsync(
            $"/ingest/{_fx.Collection}",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        Assert.True(resp.Headers.Contains("Location"),
            "202 response must carry a Location header");

        var body = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("operationId", out var opIdEl));
        Assert.NotEmpty(opIdEl.GetString()!);

        Assert.True(body.RootElement.TryGetProperty("statusUrl", out var urlEl));
        Assert.Contains($"/ingest/{_fx.Collection}/operations/", urlEl.GetString());
    }

    [SkippableFact]
    public async Task Upload_Returns400_WhenRelPathMissing()
    {
        Skip.If(!_fx.IsAvailable, _fx.SkipReason);

        var payload = JsonSerializer.Serialize(new { relPath = "", content = "# Test" });

        using var resp = await _fx.Client!.PostAsync(
            $"/ingest/{_fx.Collection}",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [SkippableFact]
    public async Task Upload_Returns400_WhenContentMissing()
    {
        Skip.If(!_fx.IsAvailable, _fx.SkipReason);

        var payload = JsonSerializer.Serialize(new { relPath = "docs/test.md", content = "" });

        using var resp = await _fx.Client!.PostAsync(
            $"/ingest/{_fx.Collection}",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── Full upload→index→verify pipeline ────────────────────────────────────

    [SkippableFact]
    public async Task Upload_FullPipeline_ContentIsIndexedInQdrant()
    {
        Skip.If(!_fx.IsAvailable, _fx.SkipReason);

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

        // Verify the content point was written to Qdrant.
        var doc = await _fx.Store!.FetchContentAsync(_fx.Collection!, relPath);
        Assert.NotNull(doc);
        Assert.Contains("Hexagonal Architecture", doc!.Content);
        Assert.Equal(relPath, doc.RelPath);
    }

    [SkippableFact]
    public async Task Upload_FullPipeline_ChunkCountIsPositive()
    {
        Skip.If(!_fx.IsAvailable, _fx.SkipReason);

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

    [SkippableFact]
    public async Task PollStatus_TransitionsFromQueued_ToCompleted()
    {
        Skip.If(!_fx.IsAvailable, _fx.SkipReason);

        var payload = JsonSerializer.Serialize(new
        {
            relPath = "docs/concepts/ddd.md",
            content = "# Domain-Driven Design\n\nModel your domain, not your database.",
        });

        using var postResp = await _fx.Client!.PostAsync(
            $"/ingest/{_fx.Collection}",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Accepted, postResp.StatusCode);

        var postBody  = JsonDocument.Parse(await postResp.Content.ReadAsStringAsync());
        var opId      = postBody.RootElement.GetProperty("operationId").GetString()!;
        var statusUrl = postBody.RootElement.GetProperty("statusUrl").GetString()!;

        // Immediately after upload the status should be Queued or Processing.
        using var immediateResp = await _fx.Client.GetAsync(statusUrl);
        Assert.Equal(HttpStatusCode.OK, immediateResp.StatusCode);
        var immediateDoc    = JsonDocument.Parse(await immediateResp.Content.ReadAsStringAsync());
        var immediateStatus = immediateDoc.RootElement.GetProperty("status").GetString();
        Assert.True(
            immediateStatus is "Queued" or "Processing",
            $"Expected Queued or Processing immediately after upload, got {immediateStatus}");

        // Wait for completion.
        var finalDoc = await _fx.UploadAndWaitAsync(
            _fx.Collection!,
            "docs/concepts/ddd-warm-up.md",   // second file to ensure worker is alive
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

    [SkippableFact]
    public async Task PollStatus_Returns404_ForUnknownOperationId()
    {
        Skip.If(!_fx.IsAvailable, _fx.SkipReason);

        using var resp = await _fx.Client!.GetAsync(
            $"/ingest/{_fx.Collection}/operations/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── GET /ingest/{collection}/operations (list) ────────────────────────────

    [SkippableFact]
    public async Task ListOperations_ReturnsUploadedOperations()
    {
        Skip.If(!_fx.IsAvailable, _fx.SkipReason);

        // Upload two documents.
        await _fx.UploadAndWaitAsync(_fx.Collection!, "docs/list/a.md", "# Doc A\n\nContent A.");
        await _fx.UploadAndWaitAsync(_fx.Collection!, "docs/list/b.md", "# Doc B\n\nContent B.");

        using var resp = await _fx.Client!.GetAsync($"/ingest/{_fx.Collection}/operations");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var list = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var items = list.RootElement.EnumerateArray().ToList();

        // We've uploaded at least 2 in this fixture lifecycle.
        Assert.True(items.Count >= 2, $"Expected >= 2 operations, got {items.Count}");
        Assert.All(items, item =>
            Assert.Equal(_fx.Collection, item.GetProperty("collection").GetString()));
    }

    // ── GET /admin/stats ──────────────────────────────────────────────────────

    [SkippableFact]
    public async Task AdminStats_Returns200_WithQueueDepthAndRetention()
    {
        Skip.If(!_fx.IsAvailable, _fx.SkipReason);

        using var resp = await _fx.Client!.GetAsync("/admin/stats");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("queue_depth", out _),
            "stats must include 'queue_depth'");
        Assert.True(doc.RootElement.TryGetProperty("retention_hours", out _),
            "stats must include 'retention_hours'");
    }

    // ── Re-upload idempotency ─────────────────────────────────────────────────

    [SkippableFact]
    public async Task ReUpload_ReplacesContent_WithUpdatedVersion()
    {
        Skip.If(!_fx.IsAvailable, _fx.SkipReason);

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
