using Microsoft.Extensions.Logging.Abstractions;
using RagTools.Core;
using RagTools.Core.Ingest;

namespace RagTools.Tests;

/// <summary>
/// Unit tests for <see cref="BatchIngestService"/>.
/// Validates the enqueue facade: capacity check, IngestJob construction, channel write,
/// OperationStore registration, and discriminated <see cref="BatchIngestOutcome"/> mapping.
/// </summary>
public sealed class BatchIngestServiceTests
{
    private static BatchIngestService CreateService(int channelCapacity, out IngestChannel channel, out OperationStore operations)
    {
        channel    = new IngestChannel(channelCapacity);
        operations = new OperationStore();
        return new BatchIngestService(channel, operations, NullLogger<BatchIngestService>.Instance);
    }

    private static BatchDocument Doc(string relPath = "test.md", string content = "# T\n\nbody")
        => new(relPath, content);

    private static BatchIngestResponse AssertSuccess(BatchIngestOutcome outcome)
    {
        var success = Assert.IsType<BatchIngestOutcome.Success>(outcome);
        return success.Response;
    }

    [Fact]
    public void Enqueue_EmptyDocuments_ReturnsSuccessWithEmptyOpList()
    {
        var sut = CreateService(10, out _, out _);

        var outcome = sut.Enqueue(new BatchIngestRequest(
            Collection: "test", Documents: [], BatchRules: null, Warnings: []));

        var response = AssertSuccess(outcome);
        Assert.Empty(response.Operations);
        Assert.Equal(0, response.Count);
        Assert.StartsWith("batch:test:", response.BatchId);
    }

    [Fact]
    public void Enqueue_SingleDocument_WritesJobToChannel()
    {
        var sut = CreateService(10, out var channel, out _);

        var outcome = sut.Enqueue(new BatchIngestRequest(
            Collection: "docs", Documents: [Doc("a.md", "# A")], BatchRules: null, Warnings: []));

        var response = AssertSuccess(outcome);
        Assert.Equal(1, response.Count);
        Assert.Equal(1, channel.PendingCount);
    }

    [Fact]
    public void Enqueue_RegistersOperationInStore()
    {
        var sut = CreateService(10, out _, out var operations);

        var outcome = sut.Enqueue(new BatchIngestRequest(
            Collection: "docs", Documents: [Doc("a.md")], BatchRules: null, Warnings: []));

        var response = AssertSuccess(outcome);
        var opId     = response.Operations[0].OperationId;
        var op       = operations.Get(opId);
        Assert.NotNull(op);
        Assert.Equal("docs", op!.Collection);
        Assert.Equal("a.md", op.RelPath);
    }

    [Fact]
    public void Enqueue_OperationIdEncodesCollectionAndRelPath()
    {
        var sut = CreateService(10, out _, out _);

        var outcome = sut.Enqueue(new BatchIngestRequest(
            Collection: "mycoll",
            Documents:  [Doc("sub/folder/file.md")],
            BatchRules: null,
            Warnings:   []));

        var response = AssertSuccess(outcome);
        Assert.StartsWith("mycoll:sub-folder-file.md:", response.Operations[0].OperationId);
    }

    [Fact]
    public void Enqueue_AppendsIndexSuffixForUniqueIds()
    {
        var sut = CreateService(10, out _, out _);

        var outcome = sut.Enqueue(new BatchIngestRequest(
            Collection: "c",
            Documents:  [Doc("a.md"), Doc("b.md"), Doc("c.md")],
            BatchRules: null,
            Warnings:   []));

        var response = AssertSuccess(outcome);
        Assert.EndsWith("-0", response.Operations[0].OperationId);
        Assert.EndsWith("-1", response.Operations[1].OperationId);
        Assert.EndsWith("-2", response.Operations[2].OperationId);
    }

    [Fact]
    public void Enqueue_PreservesRelPathInResponse()
    {
        var sut = CreateService(10, out _, out _);

        var outcome = sut.Enqueue(new BatchIngestRequest(
            Collection: "c", Documents: [Doc("docs/foo.md")], BatchRules: null, Warnings: []));

        var response = AssertSuccess(outcome);
        Assert.Equal("docs/foo.md", response.Operations[0].RelPath);
    }

    [Fact]
    public void Enqueue_StatusUrlMatchesRoutePattern()
    {
        var sut = CreateService(10, out _, out _);

        var outcome = sut.Enqueue(new BatchIngestRequest(
            Collection: "docs", Documents: [Doc("a.md")], BatchRules: null, Warnings: []));

        var response = AssertSuccess(outcome);
        var entry    = response.Operations[0];
        var expected = $"/ingest/docs/operations/{Uri.EscapeDataString(entry.OperationId)}";
        Assert.Equal(expected, entry.StatusUrl);
    }

    [Fact]
    public void Enqueue_PropagatesWarnings()
    {
        var sut = CreateService(10, out _, out _);

        var warnings = new[] { "skipped foo", "missing bar" };
        var outcome = sut.Enqueue(new BatchIngestRequest(
            Collection: "c", Documents: [Doc()], BatchRules: null, Warnings: warnings));

        var response = AssertSuccess(outcome);
        Assert.Equal(warnings, response.Warnings);
    }

    [Fact]
    public void Enqueue_QueueAtCapacity_ReturnsFailureWithQueueFullError()
    {
        var sut = CreateService(2, out var channel, out _);
        sut.Enqueue(new BatchIngestRequest("c", [Doc("a.md"), Doc("b.md")], null, []));
        Assert.Equal(2, channel.PendingCount);

        var outcome = sut.Enqueue(new BatchIngestRequest("c", [Doc("c.md")], null, []));

        var failure = Assert.IsType<BatchIngestOutcome.Failure>(outcome);
        Assert.Equal(BatchIngestError.QueueFull, failure.Error);
        Assert.NotNull(failure.Details);
        Assert.Equal(2, Assert.IsType<int>(failure.Details!["pending"]));
        Assert.Equal(2, Assert.IsType<int>(failure.Details["capacity"]));
        Assert.Equal(1, Assert.IsType<int>(failure.Details["incoming"]));
    }

    [Fact]
    public void Enqueue_QueueFull_DoesNotEnqueueAnyJob()
    {
        var sut = CreateService(2, out var channel, out var operations);
        sut.Enqueue(new BatchIngestRequest("c", [Doc("a.md"), Doc("b.md")], null, []));

        sut.Enqueue(new BatchIngestRequest("c", [Doc("c.md"), Doc("d.md")], null, []));

        Assert.Equal(2, channel.PendingCount);
        Assert.Equal(2, operations.GetByCollection("c").Count);
    }

    [Fact]
    public void Enqueue_BatchIdIncludesCollectionAndTicks()
    {
        var sut = CreateService(10, out _, out _);

        var outcome = sut.Enqueue(new BatchIngestRequest("docs", [Doc()], null, []));

        var response = AssertSuccess(outcome);
        Assert.Matches(@"^batch:docs:\d+$", response.BatchId);
    }

    [Fact]
    public void Enqueue_CancelledMidLoop_ThrowsOperationCanceled()
    {
        var sut = CreateService(10, out _, out _);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            sut.Enqueue(new BatchIngestRequest("c", [Doc("a.md")], null, []), cts.Token));
    }

    [Fact]
    public void Enqueue_WrittenJob_CarriesAllRequestFields()
    {
        var sut = CreateService(10, out var channel, out _);

        var rules = RagConfig.ParseMetadataRules("""
            adr_id_patterns:
              - pattern: "adr/(?P<id>\\d{4})/"
            doc_kind_rules:
              - glob: "docs/adr/**"
                kind: "adr_main"
            """);

        var beforeTicks = DateTimeOffset.UtcNow.Ticks;
        sut.Enqueue(new BatchIngestRequest(
            Collection: "mycoll",
            Documents:  [new BatchDocument("docs/adr/0007/0007-x.md", "# X\n\nbody")],
            BatchRules: rules,
            Warnings:   []));
        var afterTicks = DateTimeOffset.UtcNow.Ticks;

        Assert.True(channel.Reader.TryRead(out var job));
        Assert.NotNull(job);
        Assert.Equal("mycoll", job!.Collection);
        Assert.Equal("docs/adr/0007/0007-x.md", job.RelPath);
        Assert.Equal("# X\n\nbody", job.Content);
        Assert.Equal("adr_main", job.DocKind);
        Assert.Equal("0007", job.AdrId);
        Assert.StartsWith("mycoll:docs-adr-0007-0007-x.md:", job.OperationId);
        Assert.EndsWith("-0", job.OperationId);
        Assert.InRange(job.EnqueuedAt.Ticks, beforeTicks, afterTicks);
    }

    [Fact]
    public void Enqueue_NoBatchRules_LeavesDocKindAndAdrIdNull()
    {
        var sut = CreateService(10, out var channel, out _);

        sut.Enqueue(new BatchIngestRequest(
            "c", [Doc("anything.md", "x")], BatchRules: null, Warnings: []));

        Assert.True(channel.Reader.TryRead(out var job));
        Assert.Null(job!.DocKind);
        Assert.Null(job.AdrId);
    }
}
