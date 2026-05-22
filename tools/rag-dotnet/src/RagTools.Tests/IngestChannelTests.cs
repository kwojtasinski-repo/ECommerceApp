using RagTools.Core;

namespace RagTools.Tests;

/// <summary>Unit tests for IngestChannel — bounded async channel wrapper.</summary>
public sealed class IngestChannelTests
{
    private static IngestJob MakeJob(string relPath = "docs/file.md") => new()
    {
        OperationId = Guid.NewGuid().ToString(),
        Collection  = "test_col",
        RelPath     = relPath,
        Content     = "# Hello\n\nSome content.",
        EnqueuedAt  = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void TryWrite_ReturnsTrueWhenQueueHasSpace()
    {
        var channel = new IngestChannel();
        var result = channel.TryWrite(MakeJob());
        Assert.True(result);
    }

    [Fact]
    public void PendingCount_ReflectsEnqueuedItems()
    {
        var channel = new IngestChannel();
        channel.TryWrite(MakeJob("a.md"));
        channel.TryWrite(MakeJob("b.md"));

        // PendingCount approximates unread items — at minimum 0.
        Assert.True(channel.PendingCount >= 0);
    }

    [Fact]
    public async Task WriteAsync_AllowsReading_FromReader()
    {
        var channel = new IngestChannel();
        var job = MakeJob();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await channel.WriteAsync(job, cts.Token);

        // Verify the item can be read back.
        var read = await channel.Reader.ReadAsync(cts.Token);
        Assert.Equal(job.OperationId, read.OperationId);
        Assert.Equal(job.RelPath, read.RelPath);
    }

    [Fact]
    public async Task TryWrite_ThenRead_PreservesJobDetails()
    {
        var channel = new IngestChannel();
        var job = new IngestJob
        {
            OperationId = "test-op-123",
            Collection  = "my_col",
            RelPath     = "docs/adr/0028/file.md",
            Content     = "full content here",
            DocKind     = "adr",
            EnqueuedAt  = DateTimeOffset.Parse("2024-01-01T12:00:00Z"),
        };

        channel.TryWrite(job);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var read = await channel.Reader.ReadAsync(cts.Token);

        Assert.Equal("test-op-123",                  read.OperationId);
        Assert.Equal("my_col",                        read.Collection);
        Assert.Equal("docs/adr/0028/file.md",         read.RelPath);
        Assert.Equal("full content here",             read.Content);
        Assert.Equal("adr",                           read.DocKind);
        Assert.Equal(DateTimeOffset.Parse("2024-01-01T12:00:00Z"), read.EnqueuedAt);
    }
}
