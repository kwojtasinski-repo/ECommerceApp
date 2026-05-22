using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using RagTools.Mcp.Middleware;

namespace RagTools.Tests;

/// <summary>
/// Unit tests for ApiKeyMiddleware.
///
/// Tests the middleware in isolation using DefaultHttpContext — no web host needed.
/// The configured key is injected via the constructor parameter so tests are always-run
/// and do not depend on the RAG_API_KEY environment variable being set.
/// </summary>
public sealed class ApiKeyMiddlewareTests
{
    private static ApiKeyMiddleware CreateMiddleware(RequestDelegate next, string? configuredKey = null) =>
        new(next, NullLogger<ApiKeyMiddleware>.Instance, configuredKey);

    private static DefaultHttpContext MakeContext(string path, string? apiKey = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        if (apiKey is not null)
            ctx.Request.Headers["X-Api-Key"] = apiKey;
        ctx.Response.Body = new System.IO.MemoryStream();
        return ctx;
    }

    [Fact]
    public async Task NonProtectedPath_PassesThrough_Unconditionally()
    {
        var reached = false;
        var mw = CreateMiddleware(_ => { reached = true; return Task.CompletedTask; });
        var ctx = MakeContext("/mcp");  // not /ingest or /admin

        await mw.InvokeAsync(ctx);

        Assert.True(reached);
    }

    [Fact]
    public async Task NonProtectedPath_Returns200_WhenHandlerSetsIt()
    {
        var mw = CreateMiddleware(c => { c.Response.StatusCode = 200; return Task.CompletedTask; });
        var ctx = MakeContext("/some/other/path");

        await mw.InvokeAsync(ctx);

        Assert.Equal(200, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task IngestPath_Returns401_WhenNoKeyProvided()
    {
        // Inject a configured key so auth is enforced — no env var needed.
        var reached = false;
        var mw = CreateMiddleware(_ => { reached = true; return Task.CompletedTask; }, configuredKey: "test-secret");
        var ctx = MakeContext("/ingest/mycol"); // no X-Api-Key header

        await mw.InvokeAsync(ctx);

        Assert.Equal(401, ctx.Response.StatusCode);
        Assert.False(reached);
    }

    [Fact]
    public async Task AdminPath_Returns401_WhenWrongKeyProvided()
    {
        var reached = false;
        var mw = CreateMiddleware(_ => { reached = true; return Task.CompletedTask; }, configuredKey: "test-secret");
        var ctx = MakeContext("/admin/stats", apiKey: "wrong-key");

        await mw.InvokeAsync(ctx);

        Assert.Equal(401, ctx.Response.StatusCode);
        Assert.False(reached);
    }

    [Fact]
    public async Task IngestPath_PassesThrough_InDevMode()
    {
        // Dev mode: null configuredKey means no auth enforced.
        var reached = false;
        var mw = CreateMiddleware(_ => { reached = true; return Task.CompletedTask; }, configuredKey: null);
        var ctx = MakeContext("/ingest/mycol");

        await mw.InvokeAsync(ctx);

        Assert.True(reached);
    }
}
