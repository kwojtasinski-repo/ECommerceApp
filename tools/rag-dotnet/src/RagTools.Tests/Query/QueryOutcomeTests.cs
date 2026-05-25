using RagTools.Core.Query;

namespace RagTools.Tests.Query;

public sealed class QueryOutcomeTests
{
    [Fact]
    public void Success_HoldsResponse()
    {
        var response = new QueryResponse(
            Collection: "docs",
            Question: "what is hexagonal?",
            Hits: new[]
            {
                new QueryHit(1, 0.92, "doc", "docs/concepts/hex.md", "concepts > hex", 1, "Hexagonal architecture..."),
                new QueryHit(2, 0.81, "doc", "docs/concepts/ports.md", "concepts > ports", 1, "Ports and adapters..."),
            },
            TotalCandidates: 12);

        QueryOutcome outcome = new QueryOutcome.Success(response);

        var success = Assert.IsType<QueryOutcome.Success>(outcome);
        Assert.Equal(2, success.Response.Hits.Count);
        Assert.Equal(12, success.Response.TotalCandidates);
        Assert.Equal("docs", success.Response.Collection);
    }

    [Fact]
    public void Failure_HoldsErrorAndMessage()
    {
        QueryOutcome outcome = new QueryOutcome.Failure(
            QueryError.EmptyQuestion,
            "Question must not be empty");

        var failure = Assert.IsType<QueryOutcome.Failure>(outcome);
        Assert.Equal(QueryError.EmptyQuestion, failure.Error);
        Assert.Equal("Question must not be empty", failure.Message);
        Assert.Null(failure.Details);
    }

    [Fact]
    public void Failure_CarriesOptionalDetails()
    {
        var details = new Dictionary<string, object?> { ["topK"] = 100, ["max"] = 20 };
        var failure = new QueryOutcome.Failure(QueryError.TopKOutOfRange, "top_k=100 exceeds max 20", details);

        Assert.NotNull(failure.Details);
        Assert.Equal(100, failure.Details!["topK"]);
        Assert.Equal(20, failure.Details["max"]);
    }

    [Fact]
    public void SwitchExpression_IsExhaustiveOnSuccessAndFailure()
    {
        QueryOutcome outcome = new QueryOutcome.Success(
            new QueryResponse("docs", "q", Array.Empty<QueryHit>(), TotalCandidates: 0));

        var status = outcome switch
        {
            QueryOutcome.Success _ => "ok",
            QueryOutcome.Failure _ => "err",
            _ => "unknown",
        };

        Assert.Equal("ok", status);
    }

    [Fact]
    public void QueryHit_PreservesRankAndScore()
    {
        var hit = new QueryHit(3, 0.755, "adr", "docs/adr/0001.md", "adr > 0001", 1, "decision...");

        Assert.Equal(3, hit.Rank);
        Assert.Equal(0.755, hit.Score);
        Assert.Equal("adr", hit.DocKind);
    }

    [Fact]
    public void QueryError_EnumHasExpectedValues()
    {
        // Tripwire: when a new error is added the consumer-side mapping tests must be updated.
        var values = Enum.GetValues<QueryError>();
        Assert.Equal(6, values.Length);
        Assert.Contains(QueryError.EmptyQuestion, values);
        Assert.Contains(QueryError.TopKOutOfRange, values);
        Assert.Contains(QueryError.UnknownCollection, values);
        Assert.Contains(QueryError.EmbeddingFailed, values);
        Assert.Contains(QueryError.StoreSearchFailed, values);
        Assert.Contains(QueryError.PostprocessorFailed, values);
    }
}
