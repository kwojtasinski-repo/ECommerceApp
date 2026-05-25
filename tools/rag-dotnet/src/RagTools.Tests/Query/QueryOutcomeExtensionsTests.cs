using Microsoft.AspNetCore.Mvc;
using RagTools.Core.Query;
using RagTools.Mcp.Query;

namespace RagTools.Tests.Query;

public class QueryOutcomeExtensionsTests
{
    [Fact]
    public void Success_Returns200OkWithResponseBody()
    {
        var response = new QueryResponse(
            Collection: "docs",
            Question: "hex?",
            Hits: [new QueryHit(1, 0.9, "doc", "a.md", "a", "txt")],
            TotalCandidates: 5);

        var result = new QueryOutcome.Success(response).ToActionResult();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        Assert.Same(response, ok.Value);
    }

    [Theory]
    [InlineData(QueryError.EmptyQuestion,       400)]
    [InlineData(QueryError.TopKOutOfRange,      400)]
    [InlineData(QueryError.UnknownCollection,   404)]
    [InlineData(QueryError.EmbeddingFailed,     502)]
    [InlineData(QueryError.StoreSearchFailed,   502)]
    [InlineData(QueryError.PostprocessorFailed, 500)]
    public void StatusFor_MapsEveryEnumValue(QueryError error, int expected)
    {
        Assert.Equal(expected, QueryOutcomeExtensions.StatusFor(error));
    }

    [Fact]
    public void StatusFor_PinsEveryEnumValue_NoOrphans()
    {
        // Tripwire: when a new QueryError is added, the InlineData table above
        // must grow too — otherwise this assertion catches the gap.
        var declared = Enum.GetValues<QueryError>().Length;
        Assert.Equal(6, declared);
    }

    [Fact]
    public void Failure_EmitsErrorAndCodeEnvelope()
    {
        var outcome = new QueryOutcome.Failure(
            QueryError.TopKOutOfRange,
            "top_k=100 exceeds max 20",
            new Dictionary<string, object?> { ["topK"] = 100, ["max"] = 20 });

        var result = outcome.ToActionResult();
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, obj.StatusCode);

        var body = Assert.IsAssignableFrom<IDictionary<string, object?>>(obj.Value);
        Assert.Equal("top_k=100 exceeds max 20", body["error"]);
        Assert.Equal("TopKOutOfRange",            body["code"]);
        Assert.True(body.ContainsKey("details"));
    }

    [Fact]
    public void Failure_OmitsDetailsKey_WhenNullOrEmpty()
    {
        var result = new QueryOutcome.Failure(
            QueryError.EmptyQuestion, "empty").ToActionResult();
        var obj = Assert.IsType<ObjectResult>(result);
        var body = Assert.IsAssignableFrom<IDictionary<string, object?>>(obj.Value);
        Assert.False(body.ContainsKey("details"));
    }

    [Fact]
    public void Failure_OmitsDetailsKey_WhenEmptyDictionary()
    {
        var result = new QueryOutcome.Failure(
            QueryError.EmptyQuestion, "empty",
            new Dictionary<string, object?>()).ToActionResult();
        var obj = Assert.IsType<ObjectResult>(result);
        var body = Assert.IsAssignableFrom<IDictionary<string, object?>>(obj.Value);
        Assert.False(body.ContainsKey("details"));
    }
}
