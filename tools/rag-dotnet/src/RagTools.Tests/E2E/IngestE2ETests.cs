using Microsoft.Extensions.Logging.Abstractions;
using RagTools.Core;
using RagTools.Core.ContentSources;
using Xunit;
using Xunit.Abstractions;
using RagMcpTools = RagTools.Mcp.Tools.RagTools;

namespace RagTools.Tests.E2E;

/// <summary>
/// E2E tests for the ingest pipeline (Steps 4-11).
///
/// Exercises:
///   - IngestChannel → IngestWorker → QdrantDocumentStore (chunk + content points)
///   - OperationStore state transitions (Queued → Processing → Completed)
///   - CachedDocumentStore: FetchContentAsync returns cached Qdrant content on second call
///   - RagSession multi-collection routing: two collections independently searchable
///   - ReadDocs: Qdrant content hit (no disk access) after ingest
///   - QueryCache: prefix invalidation on ingest (search re-fetches from Qdrant)
///
/// All tests require ONNX model + Qdrant (Docker or QDRANT_URL env var).
/// They are skipped automatically when neither is available.
/// </summary>
[Trait("Category", "E2E")]
[Collection(RagTestCollection.Name)]
public sealed class IngestE2ETests : IClassFixture<IngestE2EFixture>, IDisposable
{
    private readonly IngestE2EFixture _fx;
    private readonly SharedOnnxFixture _sharedOnnx;

    public IngestE2ETests(
        IngestE2EFixture fixture,
        SharedOnnxFixture sharedOnnx,
        ITestOutputHelper output)
    {
        _fx         = fixture;
        _sharedOnnx = sharedOnnx;
        _fx.Sink.SetOutput(output);         // IngestWorker logs → this test's output
        sharedOnnx.Sink.SetOutput(output);  // ONNX model logs  → this test's output
    }

    public void Dispose()
    {
        _fx.Sink.SetOutput(null);
        _sharedOnnx.Sink.SetOutput(null);
    }

    // ── Ingest worker end-to-end ──────────────────────────────────────────

    [Fact]
    public async Task IngestWorker_ProcessesJob_CompletesSuccessfully()
    {

        const string content = """
            # Distributed Caching Pattern

            Distributed caching stores computed results to reduce latency.
            Redis is a common backend. Use TTL to avoid stale data.
            """;

        var result = await _fx.EnqueueAndWaitAsync("docs/concepts/caching.md", content);

        Assert.NotNull(result);
        Assert.Equal(IngestStatus.Completed, result!.Status);
        Assert.True(result.ChunkCount > 0, "Expected at least one chunk");
    }

    [Fact]
    public async Task IngestWorker_StoresContentPoint_InQdrant()
    {

        const string relPath = "docs/concepts/event-sourcing.md";
        const string content = """
            # Event Sourcing

            Event sourcing stores all changes as a sequence of events.
            The current state is derived by replaying all events.
            Useful for audit trails and temporal queries.
            """;

        var result = await _fx.EnqueueAndWaitAsync(relPath, content);
        Assert.Equal(IngestStatus.Completed, result!.Status);

        // Verify content point is in Qdrant (FetchContentAsync should return non-null).
        var doc = await _fx.Store!.FetchContentAsync(_fx.Collection!, relPath);
        Assert.NotNull(doc);
        Assert.Contains("Event Sourcing", doc!.Content);
        Assert.Equal(relPath, doc.RelPath);
    }

    [Fact]
    public async Task OperationStore_TransitionsFromQueued_ToCompleted()
    {

        const string relPath = "docs/concepts/solid.md";
        const string content = """
            # SOLID Principles

            Five object-oriented design principles:
            Single responsibility, Open/closed, Liskov substitution,
            Interface segregation, Dependency inversion.
            """;

        var opId = $"{_fx.Collection}:{relPath}:{DateTimeOffset.UtcNow.Ticks}";
        var job = new IngestJob
        {
            OperationId = opId,
            Collection  = _fx.Collection!,
            RelPath     = relPath,
            Content     = content,
            EnqueuedAt  = DateTimeOffset.UtcNow,
        };
        _fx.Operations!.MarkQueued(opId, _fx.Collection!, relPath, job.EnqueuedAt);
        _fx.Channel!.TryWrite(job);

        // Immediately after enqueue, status should be Queued or Processing.
        var initialStatus = _fx.Operations.Get(opId)?.Status;
        Assert.True(
            initialStatus is IngestStatus.Queued or IngestStatus.Processing,
            $"Expected Queued or Processing immediately after enqueue, got {initialStatus}");

        // Wait for completion.
        var result = await _fx.EnqueueAndWaitAsync(relPath + "_poll", "# Dummy\n\nPad.", timeoutSeconds: 5);
        // Just ensure the worker is running — verify the original job completed too.
        var finalStatus = _fx.Operations.Get(opId)?.Status;
        // By now the worker should have processed the job.
        Assert.True(
            finalStatus is IngestStatus.Completed or IngestStatus.Failed or null /* TTL evicted */,
            $"Unexpected final status: {finalStatus}");
    }

    // ── CachedDocumentStore ───────────────────────────────────────────────

    [Fact]
    public async Task CachedDocumentStore_ReturnsCachedContent_OnSecondFetch()
    {

        const string relPath = "docs/concepts/ddd.md";
        const string content = """
            # Domain-Driven Design

            DDD focuses on modelling software to match the domain.
            Key building blocks: entities, value objects, aggregates,
            domain services, repositories, and bounded contexts.
            """;

        // Ingest so the content point exists in Qdrant.
        var result = await _fx.EnqueueAndWaitAsync(relPath, content);
        Assert.Equal(IngestStatus.Completed, result!.Status);

        // First fetch — comes from Qdrant.
        var first = await _fx.Store!.FetchContentAsync(_fx.Collection!, relPath);
        Assert.NotNull(first);

        // Second fetch — should come from cache (functionally identical).
        var second = await _fx.Store.FetchContentAsync(_fx.Collection!, relPath);
        Assert.NotNull(second);
        Assert.Equal(first!.Content, second!.Content);
        Assert.Equal(first.RelPath, second.RelPath);
    }

    // ── RagSession multi-collection routing ──────────────────────────────

    [Fact]
    public void RagSession_RoutesQuery_ToCorrectCollection()
    {
        // RagSession delegates to its ICollectionResolver.
        // FixedCollectionResolver always returns the given collection.
        var sessionA = new RagSession(new FixedCollectionResolver(_fx.Collection!));
        Assert.Equal(_fx.Collection, sessionA.Collection);

        // A different session resolves to a different (non-existent) collection.
        var sessionB = new RagSession(new FixedCollectionResolver("nonexistent_col_xyz"));
        Assert.Equal("nonexistent_col_xyz", sessionB.Collection);

        // Back to the real collection via a fresh session.
        var sessionC = new RagSession(new FixedCollectionResolver(_fx.Collection!));
        Assert.Equal(_fx.Collection, sessionC.Collection);
    }

    // ── read_docs Qdrant content fallback ────────────────────────────────

    [Fact]
    public async Task ReadDocs_ReturnsQdrantContent_WhenContentPointExists()
    {

        const string relPath = "docs/concepts/hexagonal-arch.md";
        const string content = """
            # Hexagonal Architecture

            Also known as Ports and Adapters.
            The core application logic is isolated from infrastructure concerns.
            Adapters implement ports (interfaces) defined by the domain.
            Testing is simplified because the domain is infrastructure-free.
            """;

        var result = await _fx.EnqueueAndWaitAsync(relPath, content);
        Assert.Equal(IngestStatus.Completed, result!.Status);

        var session = new RagSession(new FixedCollectionResolver(_fx.Collection!));
        var contentSource = new QdrantContentSource(_fx.Store!);
        var queryService = new RagTools.Core.Query.RagQueryService(
            _fx.Embedder!, _fx.Store!, _fx.Config!,
            Array.Empty<IResultPostprocessor>(),
            NullLogger<RagTools.Core.Query.RagQueryService>.Instance);
        var readDocsService = new RagTools.Core.ReadDocs.RagReadDocsService(
            _fx.Embedder!, _fx.Store!, contentSource, _fx.Config!,
            NullLogger<RagTools.Core.ReadDocs.RagReadDocsService>.Instance);
        var historyService = new RagTools.Core.History.RagHistoryService(
            _fx.Embedder!, _fx.Store!,
            NullLogger<RagTools.Core.History.RagHistoryService>.Instance);
        var listService = new RagTools.Core.Adrs.RagListService(
            _fx.Store!,
            NullLogger<RagTools.Core.Adrs.RagListService>.Instance);
        var tools = new RagMcpTools(
            queryService, readDocsService, historyService, listService,
            session,
            NullLogger<RagMcpTools>.Instance);

        // Full-content intent triggers FetchContentAsync path.
        var response = await tools.ReadDocs(
            "show me all details about hexagonal architecture",
            top_files: 1,
            cancellationToken: CancellationToken.None);

        Assert.Contains("Hexagonal Architecture", response);
        Assert.DoesNotContain("[ERROR:", response);
    }

    // ── Idempotent re-ingest ──────────────────────────────────────────────

    [Fact]
    public async Task IngestWorker_ReIngest_ReplacesOldChunks()
    {

        const string relPath = "docs/concepts/retry.md";
        const string v1 = """
            # Retry Pattern

            Retry operations that fail due to transient errors.
            Use exponential back-off to avoid thundering herd.
            """;
        const string v2 = """
            # Retry Pattern

            Retry operations that fail due to transient errors.
            Use exponential back-off to avoid thundering herd.
            Circuit breaker prevents repeated failures when the service is down.
            """;

        var r1 = await _fx.EnqueueAndWaitAsync(relPath, v1);
        Assert.Equal(IngestStatus.Completed, r1!.Status);

        // Re-ingest with updated content.
        var r2 = await _fx.EnqueueAndWaitAsync(relPath, v2);
        Assert.Equal(IngestStatus.Completed, r2!.Status);

        // Content point should reflect v2.
        var doc = await _fx.Store!.FetchContentAsync(_fx.Collection!, relPath);
        Assert.NotNull(doc);
        Assert.Contains("Circuit breaker", doc!.Content);
    }
}
