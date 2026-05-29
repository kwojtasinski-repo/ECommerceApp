using Microsoft.Extensions.Logging.Abstractions;
using RagTools.Core;
using RagTools.Core.Config;
using RagTools.Core.Ingest;

namespace RagTools.Tests;

/// <summary>
/// Unit tests for <see cref="BatchIngestService"/>.
/// Validates the enqueue facade: capacity check, IngestJob construction, channel write,
/// OperationStore registration, discriminated <see cref="BatchIngestOutcome"/> mapping,
/// plus the P3-1/P3-5 config-persist + cache-invalidate hooks.
/// </summary>
public sealed class BatchIngestServiceTests
{
    private static BatchIngestService CreateService(
        int channelCapacity,
        out IngestChannel channel,
        out OperationStore operations,
        out StubStore store,
        out StubConfigSource configSource,
        RagConfig? cfg = null)
    {
        channel      = new IngestChannel(channelCapacity);
        operations   = new OperationStore();
        store        = new StubStore();
        configSource = new StubConfigSource();
        return new BatchIngestService(
            channel, operations, store, configSource,
            cfg ?? new RagConfig(),
            NullLogger<BatchIngestService>.Instance);
    }

    private static BatchIngestService CreateService(int channelCapacity, out IngestChannel channel, out OperationStore operations)
        => CreateService(channelCapacity, out channel, out operations, out _, out _);

    private sealed class StubStore : IDocumentStore
    {
        public List<(string Collection, RagConfigPayload Payload)> Stored { get; } = new();
        public Func<string, RagConfigPayload, CancellationToken, Task>? OnStoreConfig { get; set; }

        public Task StoreConfigAsync(string collection, RagConfigPayload config, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (OnStoreConfig is not null) return OnStoreConfig(collection, config, ct);
            Stored.Add((collection, config));
            return Task.CompletedTask;
        }

        public Task<RagConfigPayload?> FetchConfigAsync(string collection, CancellationToken ct = default)
            => Task.FromResult<RagConfigPayload?>(null);

        public Task UpsertChunksAsync(string collection, IReadOnlyList<RagPoint> chunks, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task StoreDocumentAsync(string collection, ContentDocument doc, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task DeleteByPathsAsync(string collection, IEnumerable<string> relPaths, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(string collection, float[] queryVector, SearchOptions opts, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DocumentSearchResult>>([]);
        public Task<ContentDocument?> FetchContentAsync(string collection, string relPath, CancellationToken ct = default)
            => Task.FromResult<ContentDocument?>(null);
        public Task<IReadOnlyList<AdrSummary>> ListAdrsAsync(string collection, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AdrSummary>>([]);
        public Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task RecreateCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
            => Task.CompletedTask;
        public void Dispose() { }
    }

    private sealed class StubConfigSource : IConfigSource
    {
        public List<string> Invalidated { get; } = new();
        public ValueTask<RagConfigPayload> GetEffectiveAsync(string collection, CancellationToken ct = default)
            => ValueTask.FromResult(new RagConfigPayload());
        public void Invalidate(string collection) => Invalidated.Add(collection);
    }

    private static BatchDocument Doc(string relPath = "test.md", string content = "# T\n\nbody")
        => new(relPath, content);

    private static BatchIngestResponse AssertSuccess(BatchIngestOutcome outcome)
    {
        var success = Assert.IsType<BatchIngestOutcome.Success>(outcome);
        return success.Response;
    }

    [Fact]
    public async Task Enqueue_EmptyDocuments_ReturnsSuccessWithEmptyOpList()
    {
        var sut = CreateService(10, out _, out _);

        var outcome = await sut.EnqueueAsync(new BatchIngestRequest(
            Collection: "test", Documents: [], BatchRules: null, Warnings: []));

        var response = AssertSuccess(outcome);
        Assert.Empty(response.Operations);
        Assert.Equal(0, response.Count);
        Assert.StartsWith("batch:test:", response.BatchId);
    }

    [Fact]
    public async Task Enqueue_SingleDocument_WritesJobToChannel()
    {
        var sut = CreateService(10, out var channel, out _);

        var outcome = await sut.EnqueueAsync(new BatchIngestRequest(
            Collection: "docs", Documents: [Doc("a.md", "# A")], BatchRules: null, Warnings: []));

        var response = AssertSuccess(outcome);
        Assert.Equal(1, response.Count);
        Assert.Equal(1, channel.PendingCount);
    }

    [Fact]
    public async Task Enqueue_RegistersOperationInStore()
    {
        var sut = CreateService(10, out _, out var operations);

        var outcome = await sut.EnqueueAsync(new BatchIngestRequest(
            Collection: "docs", Documents: [Doc("a.md")], BatchRules: null, Warnings: []));

        var response = AssertSuccess(outcome);
        var opId     = response.Operations[0].OperationId;
        var op       = operations.Get(opId);
        Assert.NotNull(op);
        Assert.Equal("docs", op!.Collection);
        Assert.Equal("a.md", op.RelPath);
    }

    [Fact]
    public async Task Enqueue_OperationIdEncodesCollectionAndRelPath()
    {
        var sut = CreateService(10, out _, out _);

        var outcome = await sut.EnqueueAsync(new BatchIngestRequest(
            Collection: "mycoll",
            Documents:  [Doc("sub/folder/file.md")],
            BatchRules: null,
            Warnings:   []));

        var response = AssertSuccess(outcome);
        Assert.StartsWith("mycoll:sub-folder-file.md:", response.Operations[0].OperationId);
    }

    [Fact]
    public async Task Enqueue_AppendsIndexSuffixForUniqueIds()
    {
        var sut = CreateService(10, out _, out _);

        var outcome = await sut.EnqueueAsync(new BatchIngestRequest(
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
    public async Task Enqueue_PreservesRelPathInResponse()
    {
        var sut = CreateService(10, out _, out _);

        var outcome = await sut.EnqueueAsync(new BatchIngestRequest(
            Collection: "c", Documents: [Doc("docs/foo.md")], BatchRules: null, Warnings: []));

        var response = AssertSuccess(outcome);
        Assert.Equal("docs/foo.md", response.Operations[0].RelPath);
    }

    [Fact]
    public async Task Enqueue_StatusUrlMatchesRoutePattern()
    {
        var sut = CreateService(10, out _, out _);

        var outcome = await sut.EnqueueAsync(new BatchIngestRequest(
            Collection: "docs", Documents: [Doc("a.md")], BatchRules: null, Warnings: []));

        var response = AssertSuccess(outcome);
        var entry    = response.Operations[0];
        var expected = $"/ingest/docs/operations/{Uri.EscapeDataString(entry.OperationId)}";
        Assert.Equal(expected, entry.StatusUrl);
    }

    [Fact]
    public async Task Enqueue_PropagatesWarnings()
    {
        var sut = CreateService(10, out _, out _);

        var warnings = new[] { "skipped foo", "missing bar" };
        var outcome = await sut.EnqueueAsync(new BatchIngestRequest(
            Collection: "c", Documents: [Doc()], BatchRules: null, Warnings: warnings));

        var response = AssertSuccess(outcome);
        Assert.Equal(warnings, response.Warnings);
    }

    [Fact]
    public async Task Enqueue_QueueAtCapacity_ReturnsFailureWithQueueFullError()
    {
        var sut = CreateService(2, out var channel, out _);
        await sut.EnqueueAsync(new BatchIngestRequest("c", [Doc("a.md"), Doc("b.md")], null, []));
        Assert.Equal(2, channel.PendingCount);

        var outcome = await sut.EnqueueAsync(new BatchIngestRequest("c", [Doc("c.md")], null, []));

        var failure = Assert.IsType<BatchIngestOutcome.Failure>(outcome);
        Assert.Equal(BatchIngestError.QueueFull, failure.Error);
        Assert.NotNull(failure.Details);
        Assert.Equal(2, Assert.IsType<int>(failure.Details!["pending"]));
        Assert.Equal(2, Assert.IsType<int>(failure.Details["capacity"]));
        Assert.Equal(1, Assert.IsType<int>(failure.Details["incoming"]));
    }

    [Fact]
    public async Task Enqueue_QueueFull_DoesNotEnqueueAnyJob()
    {
        var sut = CreateService(2, out var channel, out var operations);
        await sut.EnqueueAsync(new BatchIngestRequest("c", [Doc("a.md"), Doc("b.md")], null, []));

        await sut.EnqueueAsync(new BatchIngestRequest("c", [Doc("c.md"), Doc("d.md")], null, []));

        Assert.Equal(2, channel.PendingCount);
        Assert.Equal(2, operations.GetByCollection("c").Count);
    }

    [Fact]
    public async Task Enqueue_BatchIdIncludesCollectionAndTicks()
    {
        var sut = CreateService(10, out _, out _);

        var outcome = await sut.EnqueueAsync(new BatchIngestRequest("docs", [Doc()], null, []));

        var response = AssertSuccess(outcome);
        Assert.Matches(@"^batch:docs:\d+$", response.BatchId);
    }

    [Fact]
    public async Task Enqueue_CancelledMidLoop_ThrowsOperationCanceled()
    {
        var sut = CreateService(10, out _, out _);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            sut.EnqueueAsync(new BatchIngestRequest("c", [Doc("a.md")], null, []), cts.Token));
    }

    [Fact]
    public async Task Enqueue_WrittenJob_CarriesAllRequestFields()
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
        await sut.EnqueueAsync(new BatchIngestRequest(
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
    public async Task Enqueue_NoBatchRules_LeavesDocKindAndAdrIdNull()
    {
        var sut = CreateService(10, out var channel, out _);

        await sut.EnqueueAsync(new BatchIngestRequest(
            "c", [Doc("anything.md", "x")], BatchRules: null, Warnings: []));

        Assert.True(channel.Reader.TryRead(out var job));
        Assert.Null(job!.DocKind);
        Assert.Null(job.AdrId);
    }

    // ── P3-1 / P3-5: config persistence + cache invalidation ──────────────────

    [Fact]
    public async Task Enqueue_Success_PersistsConfigPayloadToStore()
    {
        var sut = CreateService(10, out _, out _, out var store, out _);

        await sut.EnqueueAsync(new BatchIngestRequest(
            "mycoll", [Doc("a.md")], BatchRules: null, Warnings: []));

        Assert.Single(store.Stored);
        Assert.Equal("mycoll", store.Stored[0].Collection);
        Assert.Equal(2, store.Stored[0].Payload.SchemaVersion);
    }

    [Fact]
    public async Task Enqueue_Success_InvalidatesConfigSourceForCollection()
    {
        var sut = CreateService(10, out _, out _, out _, out var configSource);

        await sut.EnqueueAsync(new BatchIngestRequest(
            "mycoll", [Doc("a.md")], BatchRules: null, Warnings: []));

        Assert.Equal(new[] { "mycoll" }, configSource.Invalidated);
    }

    [Fact]
    public async Task Enqueue_QueueFull_DoesNotPersistConfigOrInvalidate()
    {
        var sut = CreateService(2, out _, out _, out var store, out var configSource);
        await sut.EnqueueAsync(new BatchIngestRequest("c", [Doc("a.md"), Doc("b.md")], null, []));
        store.Stored.Clear();
        configSource.Invalidated.Clear();

        var outcome = await sut.EnqueueAsync(new BatchIngestRequest("c", [Doc("c.md")], null, []));

        Assert.IsType<BatchIngestOutcome.Failure>(outcome);
        Assert.Empty(store.Stored);
        Assert.Empty(configSource.Invalidated);
    }

    [Fact]
    public async Task Enqueue_ConfigPersistFails_ReturnsFailureAndDoesNotEnqueueJobs()
    {
        var sut = CreateService(10, out var channel, out var operations, out var store, out var configSource);
        store.OnStoreConfig = (_, _, _) => throw new InvalidOperationException("qdrant down");

        var outcome = await sut.EnqueueAsync(new BatchIngestRequest(
            "c", [Doc("a.md")], BatchRules: null, Warnings: []));

        var failure = Assert.IsType<BatchIngestOutcome.Failure>(outcome);
        Assert.Equal(BatchIngestError.ChannelWriteFailed, failure.Error);
        Assert.Contains("qdrant down", failure.Message);
        Assert.Equal(0, channel.PendingCount);
        Assert.Empty(operations.GetByCollection("c"));
        Assert.Empty(configSource.Invalidated);
    }

    [Fact]
    public async Task Enqueue_BatchRulesAdr_OverridesDocKindFieldsInPersistedPayload()
    {
        var sut = CreateService(10, out _, out _, out var store, out _);
        var rules = RagConfig.ParseMetadataRules("""
            adr:
              adr_doc_kind: "rfc_main"
              amendment_doc_kind: "rfc_amend"
            """);

        await sut.EnqueueAsync(new BatchIngestRequest(
            "c", [Doc("a.md")], BatchRules: rules, Warnings: []));

        var payload = store.Stored[0].Payload;
        Assert.Equal("rfc_main",  payload.AdrDocKind);
        Assert.Equal("rfc_amend", payload.AmendmentDocKind);
    }

    [Fact]
    public async Task Enqueue_NoBatchRulesAdr_DoesNotOverrideDefaultPayload()
    {
        var sut = CreateService(10, out _, out _, out var store, out _);

        await sut.EnqueueAsync(new BatchIngestRequest(
            "c", [Doc("a.md")], BatchRules: null, Warnings: []));

        var payload = store.Stored[0].Payload;
        Assert.Null(payload.AdrDocKind);
        Assert.Null(payload.AmendmentDocKind);
    }
}
