using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using RagTools.Mcp.Middleware;

namespace RagTools.Tests;

/// <summary>
/// Unit tests for ApiKeyMiddleware.
///
/// Tests the middleware in isolation using DefaultHttpContext — no web host needed.
/// The configured key is read from RAG_API_KEY env var at type-init time,
/// so we cannot change it per-test. Tests rely on RAG_API_KEY being unset in the
/// test runner (dev environment), which puts the middleware in "dev mode" (allow all).
///
/// Tests that require authentication behaviour are guarded with a skip condition.
/// </summary>
public sealed class ApiKeyMiddlewareTests
{
    // Whether RAG_API_KEY is set in the current test environment.
    private static readonly bool KeyIsConfigured =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RAG_API_KEY"));

    private static ApiKeyMiddleware CreateMiddleware(RequestDelegate next) =>
        new(next, NullLogger<ApiKeyMiddleware>.Instance);

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

    [SkippableFact]
    public async Task IngestPath_Returns401_WhenNoKeyProvided()
    {
        Skip.If(!KeyIsConfigured, "RAG_API_KEY not set — middleware in dev mode, auth not enforced.");

        var reached = false;
        var mw = CreateMiddleware(_ => { reached = true; return Task.CompletedTask; });
        var ctx = MakeContext("/ingest/mycol");

        await mw.InvokeAsync(ctx);

        Assert.Equal(401, ctx.Response.StatusCode);
        Assert.False(reached);
    }

    [SkippableFact]
    public async Task AdminPath_Returns401_WhenWrongKeyProvided()
    {
        Skip.If(!KeyIsConfigured, "RAG_API_KEY not set — middleware in dev mode.");

        var reached = false;
        var mw = CreateMiddleware(_ => { reached = true; return Task.CompletedTask; });
        var ctx = MakeContext("/admin/stats", apiKey: "wrong-key");

        await mw.InvokeAsync(ctx);

        Assert.Equal(401, ctx.Response.StatusCode);
        Assert.False(reached);
    }

    [Fact]
    public async Task IngestPath_PassesThrough_InDevMode()
    {
        // In dev mode (no RAG_API_KEY), /ingest/* should be allowed.
        Skip.If(KeyIsConfigured, "RAG_API_KEY is set — this test only verifies dev mode.");

        var reached = false;
        var mw = CreateMiddleware(_ => { reached = true; return Task.CompletedTask; });
        var ctx = MakeContext("/ingest/mycol");

        await mw.InvokeAsync(ctx);

        Assert.True(reached);
    }
}
