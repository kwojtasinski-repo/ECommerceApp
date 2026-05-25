using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using RagTools.Core;
using RagTools.Core.Ingest;
using RagTools.Mcp.Controllers;

namespace RagTools.Tests;

/// <summary>
/// Unit tests for IngestController — no web host, no HTTP client.
/// The controller is instantiated directly with real collaborators (parser,
/// batch ingest service, channel, store). Per-error mappings are owned by
/// <c>BatchIngestOutcomeExtensionsTests</c>; per-rule parser behavior by
/// <c>BatchValidatorTests</c> / <c>ZipBatchParserTests</c>. These tests only
/// pin the wiring: the controller forwards request → parser → service →
/// <c>ToActionResult</c> without mangling either outcome.
/// </summary>
public sealed class IngestControllerTests
{
    private const string MinMetaRulesYaml = "doc_kind_rules:\n  - {glob: \"**\", kind: doc}\n";
    private const string MinQueriesYaml   = "named_queries:\n  - {name: default, question: test, top_k: 5}\n";

    private static IngestController CreateController(
        IngestChannel? channel = null,
        OperationStore? ops = null,
        Stream? body = null,
        string contentType = "application/zip")
    {
        channel ??= new IngestChannel();
        ops     ??= new OperationStore();

        var validator = new BatchValidator(NullLogger<BatchValidator>.Instance);
        var parser    = new ZipBatchParser(validator, NullLogger<ZipBatchParser>.Instance);
        var service   = new BatchIngestService(channel, ops, NullLogger<BatchIngestService>.Instance);

        var ctrl = new IngestController(parser, service, channel, ops);

        var httpCtx = new DefaultHttpContext();
        if (body is not null)
        {
            httpCtx.Request.Body          = body;
            httpCtx.Request.ContentLength = body.Length;
            httpCtx.Request.ContentType   = contentType;
        }
        httpCtx.Response.Body  = new MemoryStream();
        ctrl.ControllerContext = new ControllerContext { HttpContext = httpCtx };
        return ctrl;
    }

    private static MemoryStream MakeValidZip(params (string relPath, string content)[] files)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string content)
            {
                using var w = new StreamWriter(zip.CreateEntry(name).Open());
                w.Write(content);
            }
            Add("rag-config.yaml", "embedder:\n  model: BAAI/bge-m3\n");
            Add("metadata-rules.yaml", MinMetaRulesYaml);
            Add("queries.yaml", MinQueriesYaml);
            foreach (var (relPath, content) in files) Add(relPath, content);
        }
        ms.Position = 0;
        return ms;
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
    }

    [Fact]
    public void GetOperation_Returns404_WhenNotFound()
    {
        var ctrl = CreateController();
        var result = ctrl.GetOperation("any", "missing");
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void GetOperation_Returns404_WhenCollectionMismatched()
    {
        var ops = new OperationStore();
        ops.MarkQueued("op-1", "alpha", "f.md", DateTimeOffset.UtcNow);
        var ctrl = CreateController(ops: ops);

        var result = ctrl.GetOperation("beta", "op-1");
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── GET /ingest/{collection}/operations ───────────────────────────────────

    [Fact]
    public void ListOperations_ReturnsEmptyList_WhenNoneStored()
    {
        var ctrl = CreateController();
        var result = ctrl.ListOperations("anything");
        var ok = Assert.IsType<OkObjectResult>(result);
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
        var ctrl = CreateController(channel: channel);

        var result = ctrl.Stats();
        var ok = Assert.IsType<OkObjectResult>(result);
        var value = ok.Value!;
        var queueDepth = (int)value.GetType().GetProperty("queue_depth")!.GetValue(value)!;
        Assert.Equal(1, queueDepth);
    }

    [Fact]
    public void Stats_ReturnsRetentionHours_MatchingOperationStore()
    {
        var ctrl = CreateController();
        var result = ctrl.Stats();
        var ok = Assert.IsType<OkObjectResult>(result);
        var value = ok.Value!;
        var retentionHours = (double)value.GetType().GetProperty("retention_hours")!.GetValue(value)!;
        Assert.Equal(OperationStore.RetentionPeriod.TotalHours, retentionHours);
    }

    // ── POST /ingest/{collection}/batch — wiring smoke tests ──────────────────

    [Fact]
    public async Task UploadBatch_Returns202_AndEnqueuesJobs_WhenZipIsValid()
    {
        var ops      = new OperationStore();
        var zip      = MakeValidZip(("docs/intro.md", "# Intro"));
        var ctrl     = CreateController(ops: ops, body: zip);

        var result   = await ctrl.UploadBatch("myproject", CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.Equal(202, accepted.StatusCode);
        var response = Assert.IsType<BatchIngestResponse>(accepted.Value);
        Assert.Equal(1, response.Count);
        Assert.Single(response.Operations);
        Assert.Equal("docs/intro.md", response.Operations[0].RelPath);
        Assert.NotNull(ops.Get(response.Operations[0].OperationId));
    }

    [Fact]
    public async Task UploadBatch_Returns400_WithCode_WhenZipIsInvalid()
    {
        var body = new MemoryStream("not a zip"u8.ToArray());
        var ctrl = CreateController(body: body);

        var result = await ctrl.UploadBatch("myproject", CancellationToken.None);

        var obj  = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, obj.StatusCode);
        var body2 = Assert.IsAssignableFrom<IDictionary<string, object?>>(obj.Value);
        Assert.Equal("InvalidZipArchive", body2["code"]);
    }

    [Fact]
    public async Task UploadBatch_Returns400_WhenZipMissesRagConfig()
    {
        // Build a ZIP without rag-config.yaml — parser → validator should reject.
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var w = new StreamWriter(zip.CreateEntry("docs/x.md").Open());
            w.Write("# x");
        }
        ms.Position = 0;

        var ctrl   = CreateController(body: ms);
        var result = await ctrl.UploadBatch("myproject", CancellationToken.None);

        var obj  = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, obj.StatusCode);
        var body = Assert.IsAssignableFrom<IDictionary<string, object?>>(obj.Value);
        Assert.Equal("MissingRagConfigYaml", body["code"]);
    }
}
