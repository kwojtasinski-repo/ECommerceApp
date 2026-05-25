using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using RagTools.Core.Ingest;
using RagTools.Mcp.Filters;
using RagTools.Mcp.Ingest;
using RagTools.Mcp.Routing;

namespace RagTools.Tests;

public class BatchIngestOutcomeExtensionsTests
{
    [Fact]
    public void Success_Returns202AcceptedWithResponseBody()
    {
        var response = new BatchIngestResponse(
            BatchId: "batch:demo:1", Count: 1,
            Operations: [new BatchOperationEntry("op-1", "doc.md", "/ingest/demo/operations/op-1")],
            Warnings: null);

        var result = new BatchIngestOutcome.Success(response).ToActionResult();

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.Equal(202, accepted.StatusCode);
        Assert.Same(response, accepted.Value);
    }

    [Theory]
    [InlineData(BatchIngestError.EmptyBody,                        400)]
    [InlineData(BatchIngestError.InvalidZipArchive,                400)]
    [InlineData(BatchIngestError.MissingRagConfigYaml,             400)]
    [InlineData(BatchIngestError.MissingMetadataRulesYaml,         400)]
    [InlineData(BatchIngestError.MissingQueriesYaml,               400)]
    [InlineData(BatchIngestError.MissingMultilingualGlossaryYaml,  400)]
    [InlineData(BatchIngestError.MetadataRulesMissingDocKindRules, 400)]
    [InlineData(BatchIngestError.QueriesMissingNamedQueries,       400)]
    [InlineData(BatchIngestError.QueriesReferenceUnknownDocKind,   400)]
    [InlineData(BatchIngestError.PathTraversalDetected,            400)]
    [InlineData(BatchIngestError.NoMarkdownFiles,                  400)]
    [InlineData(BatchIngestError.QueueFull,                        503)]
    [InlineData(BatchIngestError.ChannelWriteFailed,               500)]
    public void StatusFor_MapsEveryEnumValue(BatchIngestError error, int expected)
    {
        Assert.Equal(expected, BatchIngestOutcomeExtensions.StatusFor(error));
    }

    [Fact]
    public void StatusFor_PinsEveryEnumValue_NoOrphans()
    {
        // Tripwire: when a new BatchIngestError is added, the InlineData table above
        // must grow too — otherwise this assertion catches the gap.
        var declared = Enum.GetValues<BatchIngestError>().Length;
        Assert.Equal(13, declared);
    }

    [Fact]
    public void Failure_EmitsErrorAndCodeEnvelope()
    {
        var outcome = new BatchIngestOutcome.Failure(
            BatchIngestError.QueueFull,
            "queue is full",
            new Dictionary<string, object?> { ["pending"] = 99 });

        var result = outcome.ToActionResult();
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, obj.StatusCode);

        var body = Assert.IsAssignableFrom<IDictionary<string, object?>>(obj.Value);
        Assert.Equal("queue is full", body["error"]);
        Assert.Equal("QueueFull",     body["code"]);
        Assert.True(body.ContainsKey("details"));
    }

    [Fact]
    public void Failure_OmitsDetailsKey_WhenNullOrEmpty()
    {
        var result = new BatchIngestOutcome.Failure(
            BatchIngestError.InvalidZipArchive, "bad zip").ToActionResult();
        var obj = Assert.IsType<ObjectResult>(result);
        var body = Assert.IsAssignableFrom<IDictionary<string, object?>>(obj.Value);
        Assert.False(body.ContainsKey("details"));
    }

    [Fact]
    public void ZipParseFailure_MapsThroughSameTable()
    {
        var failure = new ZipParseOutcome.Failure(
            BatchIngestError.MissingRagConfigYaml, "missing", null);
        var result = failure.ToActionResult();
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, obj.StatusCode);
        var body = Assert.IsAssignableFrom<IDictionary<string, object?>>(obj.Value);
        Assert.Equal("MissingRagConfigYaml", body["code"]);
    }
}

public class CollectionNameRouteConstraintTests
{
    private static bool Matches(string value)
    {
        var constraint = new CollectionNameRouteConstraint();
        var values = new RouteValueDictionary { ["collection"] = value };
        return constraint.Match(httpContext: null, route: null, "collection",
            values, RouteDirection.IncomingRequest);
    }

    [Theory]
    [InlineData("docs")]
    [InlineData("rag-prod")]
    [InlineData("a")]
    [InlineData("collection_1")]
    [InlineData("0abc")]
    public void Match_AcceptsValidNames(string name) => Assert.True(Matches(name));

    [Theory]
    [InlineData("")]
    [InlineData("-leading-dash")]
    [InlineData("_leading-underscore")]
    [InlineData("UPPER")]
    [InlineData("with space")]
    [InlineData("dot.in.name")]
    [InlineData("../etc")]
    public void Match_RejectsInvalidNames(string name) => Assert.False(Matches(name));

    [Fact]
    public void Match_RejectsMissingRouteValue()
    {
        var constraint = new CollectionNameRouteConstraint();
        Assert.False(constraint.Match(null, null, "collection",
            new RouteValueDictionary(), RouteDirection.IncomingRequest));
    }
}

public class ZipUploadFilterTests
{
    private static async Task<IActionResult?> RunAsync(
        string? contentType, long? contentLength, long? maxBytes = null)
    {
        var ctx = new DefaultHttpContext();
        if (contentType is not null) ctx.Request.ContentType = contentType;
        if (contentLength is not null) ctx.Request.ContentLength = contentLength;

        var actionCtx = new ActionContext(ctx, new RouteData(), new ActionDescriptor());
        var executing = new ActionExecutingContext(
            actionCtx, [], new Dictionary<string, object?>(), controller: new object());

        var filter = maxBytes is null ? new ZipUploadFilter() : new ZipUploadFilter { MaxBytes = maxBytes.Value };

        await filter.OnActionExecutionAsync(executing, () =>
        {
            var executed = new ActionExecutedContext(actionCtx, [], controller: new object());
            return Task.FromResult(executed);
        });

        return executing.Result;
    }

    [Theory]
    [InlineData("application/zip")]
    [InlineData("application/zip; charset=binary")]
    [InlineData("application/octet-stream")]
    [InlineData("APPLICATION/ZIP")]
    public async Task PassesThrough_WhenContentTypeIsAcceptedAndSizeOk(string ct)
    {
        var result = await RunAsync(ct, contentLength: 1024);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("text/plain")]
    [InlineData("")]
    public async Task Returns415_WhenContentTypeIsRejected(string ct)
    {
        var result = await RunAsync(ct, contentLength: 1024);
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(415, obj.StatusCode);
    }

    [Fact]
    public async Task Returns413_WhenContentLengthExceedsMax()
    {
        var result = await RunAsync("application/zip", contentLength: 200, maxBytes: 100);
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(413, obj.StatusCode);
    }

    [Fact]
    public async Task PassesThrough_WhenContentLengthIsUnknown()
    {
        // Streaming upload without Content-Length — let the parser handle it.
        var result = await RunAsync("application/zip", contentLength: null);
        Assert.Null(result);
    }
}
