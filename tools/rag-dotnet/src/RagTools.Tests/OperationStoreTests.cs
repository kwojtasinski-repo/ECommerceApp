using RagTools.Core;

namespace RagTools.Tests;

/// <summary>Unit tests for OperationStore — in-memory job lifecycle tracking.</summary>
public sealed class OperationStoreTests
{
    private readonly OperationStore _store = new();

    [Fact]
    public void MarkQueued_SetsStatusQueued()
    {
        _store.MarkQueued("op1", "col", "docs/file.md", DateTimeOffset.UtcNow);

        var result = _store.Get("op1");
        Assert.NotNull(result);
        Assert.Equal(IngestStatus.Queued, result.Status);
        Assert.Equal("op1", result.OperationId);
        Assert.Equal("col", result.Collection);
        Assert.Equal("docs/file.md", result.RelPath);
    }

    [Fact]
    public void MarkProcessing_TransitionsToProcessing()
    {
        var enqueued = DateTimeOffset.UtcNow;
        _store.MarkQueued("op2", "col", "file.md", enqueued);
        _store.MarkProcessing("op2", "col", "file.md", enqueued);

        var result = _store.Get("op2");
        Assert.Equal(IngestStatus.Processing, result!.Status);
    }

    [Fact]
    public void MarkCompleted_SetsStatusAndChunkCount()
    {
        _store.MarkQueued("op3", "col", "file.md", DateTimeOffset.UtcNow);
        _store.MarkCompleted("op3", chunkCount: 7);

        var result = _store.Get("op3");
        Assert.Equal(IngestStatus.Completed, result!.Status);
        Assert.Equal(7, result.ChunkCount);
    }

    [Fact]
    public void MarkFailed_SetsStatusAndError()
    {
        _store.MarkQueued("op4", "col", "file.md", DateTimeOffset.UtcNow);
        _store.MarkFailed("op4", "timeout error");

        var result = _store.Get("op4");
        Assert.Equal(IngestStatus.Failed, result!.Status);
        Assert.Equal("timeout error", result.ErrorMessage);
    }

    [Fact]
    public void Get_ReturnsNull_ForUnknownOperation()
    {
        var result = _store.Get("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public void GetByCollection_ReturnsOnlyMatchingCollection()
    {
        _store.MarkQueued("op-a1", "alpha", "a.md", DateTimeOffset.UtcNow);
        _store.MarkQueued("op-a2", "alpha", "b.md", DateTimeOffset.UtcNow);
        _store.MarkQueued("op-b1", "beta",  "c.md", DateTimeOffset.UtcNow);

        var alphaOps = _store.GetByCollection("alpha").ToList();
        Assert.Equal(2, alphaOps.Count);
        Assert.All(alphaOps, op => Assert.Equal("alpha", op.Collection));
    }

    [Fact]
    public void GetByCollection_ReturnsEmpty_WhenNoOpsForCollection()
    {
        var ops = _store.GetByCollection("unknown-col").ToList();
        Assert.Empty(ops);
    }

    [Fact]
    public void StateTransitions_DoNotThrow_ForUnknownOp()
    {
        // MarkProcessing: creates a new entry (processing can be called first in recovery scenarios).
        _store.MarkProcessing("ghost", "col", "file.md", DateTimeOffset.UtcNow);
        // MarkCompleted/MarkFailed: no-op when op not found in store.
        _store.MarkCompleted("ghost2", 5);    // ghost2 not in store → no-op (no throw)
        _store.MarkFailed("ghost3", "err");   // ghost3 not in store → no-op (no throw)

        // ghost was created by MarkProcessing; ghost2/ghost3 were never added.
        Assert.NotNull(_store.Get("ghost"));
        Assert.Null(_store.Get("ghost2"));
        Assert.Null(_store.Get("ghost3"));
    }
}
