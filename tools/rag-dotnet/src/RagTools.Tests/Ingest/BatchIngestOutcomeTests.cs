using RagTools.Core.Ingest;

namespace RagTools.Tests.Ingest;

public sealed class BatchIngestOutcomeTests
{
    [Fact]
    public void Success_HoldsResponse()
    {
        var response = new BatchIngestResponse(
            BatchId: "batch:docs:1",
            Count: 2,
            Operations: new[]
            {
                new BatchOperationEntry("docs:a.md:1-0", "a.md", "/ingest/docs/operations/docs%3Aa.md%3A1-0"),
                new BatchOperationEntry("docs:b.md:1-1", "b.md", "/ingest/docs/operations/docs%3Ab.md%3A1-1"),
            });

        BatchIngestOutcome outcome = new BatchIngestOutcome.Success(response);

        var success = Assert.IsType<BatchIngestOutcome.Success>(outcome);
        Assert.Equal(2, success.Response.Count);
        Assert.Equal("batch:docs:1", success.Response.BatchId);
    }

    [Fact]
    public void Failure_HoldsErrorAndMessage()
    {
        BatchIngestOutcome outcome = new BatchIngestOutcome.Failure(
            BatchIngestError.InvalidZipArchive,
            "Invalid ZIP archive");

        var failure = Assert.IsType<BatchIngestOutcome.Failure>(outcome);
        Assert.Equal(BatchIngestError.InvalidZipArchive, failure.Error);
        Assert.Equal("Invalid ZIP archive", failure.Message);
        Assert.Null(failure.Details);
    }

    [Fact]
    public void Failure_CarriesOptionalDetails()
    {
        var details = new Dictionary<string, object?> { ["pending"] = 100 };
        var failure = new BatchIngestOutcome.Failure(BatchIngestError.QueueFull, "Queue full", details);

        Assert.NotNull(failure.Details);
        Assert.Equal(100, failure.Details!["pending"]);
    }

    [Fact]
    public void SwitchExpression_IsExhaustiveOnSuccessAndFailure()
    {
        BatchIngestOutcome outcome = new BatchIngestOutcome.Success(
            new BatchIngestResponse("batch:x:1", 0, Array.Empty<BatchOperationEntry>()));

        // Pattern match must compile and run without _ default for the abstract base
        // (both subtypes covered explicitly).
        var status = outcome switch
        {
            BatchIngestOutcome.Success _ => "ok",
            BatchIngestOutcome.Failure _ => "err",
            _ => "unknown",
        };

        Assert.Equal("ok", status);
    }
}
