using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace RagTools.Mcp.Middleware;

/// <summary>
/// Catches malformed HTTP request bodies (e.g. JSON-RPC parse failures, oversize
/// payloads, invalid framing) thrown deeper in the pipeline and converts them
/// into a small JSON error envelope: <c>{"error":"...","code":"BadRequest"}</c>.
///
/// Without this middleware Kestrel / the MCP layer would propagate a 5xx with
/// the framework's default HTML page or raw exception text, which leaks
/// implementation details to the caller.
///
/// Only translates <see cref="BadHttpRequestException"/> and
/// <see cref="JsonException"/> — every other exception is rethrown so the
/// general 5xx pipeline (or upstream MCP handler) still sees it.
/// </summary>
public sealed class BadRequestEnvelopeMiddleware(RequestDelegate next)
{
    private const string MalformedBody = "{\"error\":\"Malformed request body.\",\"code\":\"BadRequest\"}";
    private const string MalformedJson = "{\"error\":\"Malformed JSON payload.\",\"code\":\"BadRequest\"}";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (BadHttpRequestException ex)
        {
            await WriteEnvelope(context,
                ex.StatusCode is >= 400 and < 500 ? ex.StatusCode : StatusCodes.Status400BadRequest,
                MalformedBody);
        }
        catch (JsonException)
        {
            await WriteEnvelope(context, StatusCodes.Status400BadRequest, MalformedJson);
        }
    }

    private static async Task WriteEnvelope(HttpContext context, int status, string body)
    {
        if (context.Response.HasStarted) return;
        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(body);
    }
}
