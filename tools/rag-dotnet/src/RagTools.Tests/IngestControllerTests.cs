using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using RagTools.Core;
using RagTools.Mcp.Controllers;

namespace RagTools.Tests;

/// <summary>
/// Unit tests for IngestController — no web host, no HTTP client.
/// The controller is instantiated directly with a real IngestChannel and OperationStore.
/// A DefaultHttpContext is wired to ControllerContext so Response.Headers is writable.
/// </summary>
public sealed class IngestControllerTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IngestController CreateController(
        IngestChannel? channel = null,
        OperationStore? ops = null)
    {
        channel ??= new IngestChannel();
        ops     ??= new OperationStore();

        var ctrl = new IngestController(channel, ops, NullLogger<IngestController>.Instance);

        // Wire up a real HttpContext so Response.Headers["Location"] = ... works.
        var httpCtx = new DefaultHttpContext();
        httpCtx.Response.Body = new System.IO.MemoryStream();
        ctrl.ControllerContext = new ControllerContext { HttpContext = httpCtx };

        return ctrl;
    }

    private static IngestChannel FullChannel()
    {
        // Capacity=1, already written-to → TryWrite returns false.
        var ch = new IngestChannel(capacity: 1);
        ch.TryWrite(new IngestJob
        {
            OperationId = "seed",
            Collection  = "col",
            RelPath     = "seed.md",
            Content     = "seed",
            EnqueuedAt  = DateTimeOffset.UtcNow,
        });
        return ch;
    }

    // ── POST /ingest/{collection} ─────────────────────────────────────────────

    [Fact]
    public void Ingest_Returns202_WithBody_WhenQueued()
    {
        var ctrl = CreateController();
        var req  = new IngestRequest { RelPath = "docs/concepts/caching.md", Content = "# Caching" };

        var result = ctrl.Ingest("myproject", req);

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.Equal(202, accepted.StatusCode);

        var body = Assert.IsType<IngestResponse>(accepted.Value);
        Assert.Equal("myproject", body.Collection);
        Assert.Equal("docs/concepts/caching.md", body.RelPath);
        Assert.NotEmpty(body.OperationId);
        Assert.StartsWith("/ingest/myproject/operations/", body.StatusUrl);
    }

    [Fact]
    public void Ingest_SetsLocationHeader_WhenQueued()
    {
        var ctrl = CreateController();
        var req  = new IngestRequest { RelPath = "docs/test.md", Content = "# Test" };

        ctrl.Ingest("myproject", req);

        Assert.True(ctrl.Response.Headers.ContainsKey("Location"));
        Assert.StartsWith("/ingest/myproject/operations/", ctrl.Response.Headers["Location"].ToString());
    }

    [Fact]
    public void Ingest_MarksOperationQueued_InOperationStore()
    {
        var ops  = new OperationStore();
        var ctrl = CreateController(ops: ops);
        var req  = new IngestRequest { RelPath = "docs/test.md", Content = "# Test" };

        var result   = ctrl.Ingest("myproject", req);
        var accepted = Assert.IsType<AcceptedResult>(result);
        var body     = Assert.IsType<IngestResponse>(accepted.Value);

        var op = ops.Get(body.OperationId);
        Assert.NotNull(op);
        Assert.Equal(IngestStatus.Queued, op!.Status);
        Assert.Equal("myproject", op.Collection);
        Assert.Equal("docs/test.md", op.RelPath);
    }

    [Fact]
    public void Ingest_WritesJobToChannel_WhenQueued()
    {
        var channel = new IngestChannel();
        var ctrl    = CreateController(channel: channel);
        var req     = new IngestRequest { RelPath = "docs/test.md", Content = "# Test" };

        ctrl.Ingest("myproject", req);

        Assert.Equal(1, channel.PendingCount);
    }

    [Fact]
    public void Ingest_Returns503_WhenChannelFull()
    {
        var ctrl = CreateController(channel: FullChannel());
        var req  = new IngestRequest { RelPath = "docs/test.md", Content = "# Test" };

        var result = ctrl.Ingest("myproject", req);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, obj.StatusCode);
    }

    [Fact]
    public void Ingest_DoesNotMarkQueued_WhenChannelFull()
    {
        var ops  = new OperationStore();
        var ctrl = CreateController(channel: FullChannel(), ops: ops);
        var req  = new IngestRequest { RelPath = "docs/test.md", Content = "# Test" };

        ctrl.Ingest("myproject", req);

        // No operation should have been registered — queue was full before MarkQueued
        Assert.Empty(ops.GetByCollection("myproject"));
    }

    [Fact]
    public void Ingest_Returns400_WhenRelPathEmpty()
    {
        var ctrl = CreateController();
        var req  = new IngestRequest { RelPath = "", Content = "# Hello" };

        var result = ctrl.Ingest("myproject", req);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void Ingest_Returns400_WhenRelPathWhitespace()
    {
        var ctrl = CreateController();
        var req  = new IngestRequest { RelPath = "   ", Content = "# Hello" };

        var result = ctrl.Ingest("myproject", req);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void Ingest_Returns400_WhenContentEmpty()
    {
        var ctrl = CreateController();
        var req  = new IngestRequest { RelPath = "docs/test.md", Content = "" };

        var result = ctrl.Ingest("myproject", req);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── GET /ingest/{collection}/operations/{opId} ────────────────────────────

    [Fact]
    public void GetOperation_Returns200_WhenFound()
    {
        var ops = new OperationStore();
        ops.MarkQueued("op-1", "myproject", "docs/test.md", DateTimeOffset.UtcNow);
        var ctrl = CreateController(ops: ops);

        var result = ctrl.GetOperation("myproject", "op-1");

        var ok = Assert.IsType<OkObjectResult>(result);
        var op = Assert.IsType<IngestOperationResult>(ok.Value);
        Assert.Equal("op-1", op.OperationId);
        Assert.Equal(IngestStatus.Queued, op.Status);
    }

    [Fact]
    public void GetOperation_Returns404_WhenNotFound()
    {
        var ctrl = CreateController();

        var result = ctrl.GetOperation("myproject", "nonexistent");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void GetOperation_Returns404_WhenCollectionMismatch()
    {
        var ops = new OperationStore();
        ops.MarkQueued("op-1", "project-a", "docs/test.md", DateTimeOffset.UtcNow);
        var ctrl = CreateController(ops: ops);

        // op-1 belongs to project-a but we ask under project-b
        var result = ctrl.GetOperation("project-b", "op-1");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void GetOperation_ReflectsCompletedStatus_AfterMarkCompleted()
    {
        var ops = new OperationStore();
        ops.MarkQueued("op-1", "myproject", "docs/test.md", DateTimeOffset.UtcNow);
        ops.MarkProcessing("op-1", "myproject", "docs/test.md", DateTimeOffset.UtcNow);
        ops.MarkCompleted("op-1", chunkCount: 7);
        var ctrl = CreateController(ops: ops);

        var result = ctrl.GetOperation("myproject", "op-1");

        var ok = Assert.IsType<OkObjectResult>(result);
        var op = Assert.IsType<IngestOperationResult>(ok.Value);
        Assert.Equal(IngestStatus.Completed, op.Status);
        Assert.Equal(7, op.ChunkCount);
    }

    [Fact]
    public void GetOperation_ReflectsFailedStatus_AfterMarkFailed()
    {
        var ops = new OperationStore();
        ops.MarkQueued("op-1", "myproject", "docs/test.md", DateTimeOffset.UtcNow);
        ops.MarkProcessing("op-1", "myproject", "docs/test.md", DateTimeOffset.UtcNow);
        ops.MarkFailed("op-1", "embedding model not found");
        var ctrl = CreateController(ops: ops);

        var result = ctrl.GetOperation("myproject", "op-1");

        var ok = Assert.IsType<OkObjectResult>(result);
        var op = Assert.IsType<IngestOperationResult>(ok.Value);
        Assert.Equal(IngestStatus.Failed, op.Status);
        Assert.Equal("embedding model not found", op.ErrorMessage);
    }

    // ── GET /ingest/{collection}/operations ──────────────────────────────────

    [Fact]
    public void ListOperations_ReturnsAll_ForMatchingCollection()
    {
        var ops     = new OperationStore();
        var enqueue = DateTimeOffset.UtcNow;
        ops.MarkQueued("op-a1", "project-a", "docs/a.md", enqueue);
        ops.MarkQueued("op-a2", "project-a", "docs/b.md", enqueue);
        ops.MarkQueued("op-b1", "project-b", "docs/c.md", enqueue);
        var ctrl = CreateController(ops: ops);

        var result = ctrl.ListOperations("project-a");

        var ok   = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<IngestOperationResult>>(ok.Value);
        Assert.Equal(2, list.Count);
        Assert.All(list, op => Assert.Equal("project-a", op.Collection));
    }

    [Fact]
    public void ListOperations_ReturnsEmpty_WhenNoOperationsForCollection()
    {
        var ctrl = CreateController();

        var result = ctrl.ListOperations("unknown-project");

        var ok   = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<IngestOperationResult>>(ok.Value);
        Assert.Empty(list);
    }

    // ── GET /admin/stats ─────────────────────────────────────────────────────

    [Fact]
    public void Stats_ReturnsQueueDepth_ReflectingPendingJobs()
    {
        var channel = new IngestChannel();
        channel.TryWrite(new IngestJob
        {
            OperationId = "j1", Collection = "c", RelPath = "f", Content = "c",
            EnqueuedAt  = DateTimeOffset.UtcNow,
        });
        channel.TryWrite(new IngestJob
        {
            OperationId = "j2", Collection = "c", RelPath = "g", Content = "c",
            EnqueuedAt  = DateTimeOffset.UtcNow,
        });
        var ctrl = CreateController(channel: channel);

        var result = ctrl.Stats();

        var ok    = Assert.IsType<OkObjectResult>(result);
        var value = ok.Value!;
        var queueDepth = (int)value.GetType().GetProperty("queue_depth")!.GetValue(value)!;
        Assert.Equal(2, queueDepth);
    }

    [Fact]
    public void Stats_ReturnsRetentionHours_MatchingOperationStore()
    {
        var ctrl = CreateController();

        var result = ctrl.Stats();

        var ok    = Assert.IsType<OkObjectResult>(result);
        var value = ok.Value!;
        var retentionHours = (double)value.GetType().GetProperty("retention_hours")!.GetValue(value)!;
        Assert.Equal(OperationStore.RetentionPeriod.TotalHours, retentionHours);
    }

    // ── POST /ingest/{collection}/batch  (P2-2) ───────────────────────────────

    private static System.IO.MemoryStream MakeZip(params (string relPath, string content)[] files)
    {
        var ms = new System.IO.MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (relPath, content) in files)
            {
                var entry = zip.CreateEntry(relPath);
                using var w = new System.IO.StreamWriter(entry.Open());
                w.Write(content);
            }
        }
        ms.Position = 0;
        return ms;
    }

    private static IngestController CreateControllerWithBody(System.IO.Stream body, IngestChannel? channel = null, OperationStore? ops = null)
    {
        channel ??= new IngestChannel();
        ops     ??= new OperationStore();

        var ctrl = new IngestController(channel, ops, NullLogger<IngestController>.Instance);

        var httpCtx = new DefaultHttpContext();
        httpCtx.Request.Body          = body;
        httpCtx.Request.ContentLength = body.Length;
        httpCtx.Response.Body         = new System.IO.MemoryStream();
        ctrl.ControllerContext = new ControllerContext { HttpContext = httpCtx };

        return ctrl;
    }

    [Fact]
    public async Task UploadBatch_Returns202_WithCountAndOperations()
    {
        var zip  = MakeZip(("docs/intro.md", "# Intro"));
        var ctrl = CreateControllerWithBody(zip);

        var result = await ctrl.UploadBatch("col");

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.Equal(202, accepted.StatusCode);
        var body = accepted.Value!;
        var count = (int)body.GetType().GetProperty("Count")!.GetValue(body)!;
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task UploadBatch_MultipleFiles_EnqueuesAll()
    {
        var channel = new IngestChannel();
        var zip = MakeZip(
            ("docs/adr/0001.md", "# ADR-0001"),
            ("docs/adr/0002.md", "# ADR-0002"),
            ("docs/concepts/ddd.md", "# DDD"));
        var ctrl = CreateControllerWithBody(zip, channel: channel);

        var result = await ctrl.UploadBatch("myproject");

        Assert.IsType<AcceptedResult>(result);
        Assert.Equal(3, channel.PendingCount);
    }

    [Fact]
    public async Task UploadBatch_EachFileGetsUniqueOperationId()
    {
        var ops = new OperationStore();
        var zip = MakeZip(("a.md", "A"), ("b.md", "B"));
        var ctrl = CreateControllerWithBody(zip, ops: ops);

        var result = await ctrl.UploadBatch("col");

        var accepted = Assert.IsType<AcceptedResult>(result);
        var body     = accepted.Value!;
        dynamic ops2 = body.GetType().GetProperty("Operations")!.GetValue(body)!;
        // Cast to IEnumerable to iterate
        var opList = (System.Collections.IEnumerable)ops2;
        var ids = new System.Collections.Generic.List<string>();
        foreach (var op in opList)
            ids.Add((string)op.GetType().GetProperty("OperationId")!.GetValue(op)!);
        Assert.Equal(2, ids.Distinct().Count());
    }

    [Fact]
    public async Task UploadBatch_RegistersOperationsInStore()
    {
        var ops = new OperationStore();
        var zip = MakeZip(("f.md", "# F"));
        var ctrl = CreateControllerWithBody(zip, ops: ops);

        var result = await ctrl.UploadBatch("col");

        var accepted = Assert.IsType<AcceptedResult>(result);
        var body     = accepted.Value!;
        dynamic opList2 = body.GetType().GetProperty("Operations")!.GetValue(body)!;
        var opList = (System.Collections.IEnumerable)opList2;
        foreach (var op in opList)
        {
            var opId = (string)op.GetType().GetProperty("OperationId")!.GetValue(op)!;
            Assert.NotNull(ops.Get(opId));
            Assert.Equal(IngestStatus.Queued, ops.Get(opId)!.Status);
        }
    }

    [Fact]
    public async Task UploadBatch_InvalidZip_Returns400()
    {
        var body = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes("not a zip"));
        var ctrl = CreateControllerWithBody(body);

        var result = await ctrl.UploadBatch("col");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadBatch_EmptyZip_Returns400()
    {
        var zip  = MakeZip(); // no files
        var ctrl = CreateControllerWithBody(zip);

        var result = await ctrl.UploadBatch("col");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadBatch_QueueFull_Returns503()
    {
        var ctrl = CreateControllerWithBody(MakeZip(("a.md", "A"), ("b.md", "B")), channel: FullChannel());

        var result = await ctrl.UploadBatch("col");

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, objResult.StatusCode);
    }
}
