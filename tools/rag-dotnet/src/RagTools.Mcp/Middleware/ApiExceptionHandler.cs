using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RagTools.Mcp.Tools;

namespace RagTools.Mcp.Middleware;

/// <summary>
/// Global last-resort handler for uncaught exceptions raised inside ASP.NET
/// controller actions (and any other request that reaches the endpoint
/// pipeline). Returns a sanitized JSON envelope and a 5xx status, never the
/// framework's default HTML stack-trace page.
///
/// Path-sanitisation reuses <see cref="ToolErrorSanitizer.Sanitize"/> so the
/// REST API and the MCP tool surface produce the same shape of error response
/// (the only differences are the JSON envelope and the HTTP status code).
///
/// Registered via <c>builder.Services.AddExceptionHandler&lt;ApiExceptionHandler&gt;()</c>
/// + <c>app.UseExceptionHandler()</c> in <c>Program.cs</c>.
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Cancellation is not an error — let the framework finalise the response.
        if (exception is OperationCanceledException) return false;

        logger.LogError(exception, "Unhandled exception in {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        if (httpContext.Response.HasStarted) return false;

        var (status, code) = exception switch
        {
            BadHttpRequestException b => (b.StatusCode is >= 400 and < 500 ? b.StatusCode : StatusCodes.Status400BadRequest, "BadRequest"),
            ArgumentException         => (StatusCodes.Status400BadRequest, "BadRequest"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            NotImplementedException   => (StatusCodes.Status501NotImplemented, "NotImplemented"),
            _                         => (StatusCodes.Status500InternalServerError, "InternalServerError"),
        };

        httpContext.Response.Clear();
        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new
        {
            error = ToolErrorSanitizer.Sanitize(exception),
            code,
        });
        await httpContext.Response.WriteAsync(payload, cancellationToken);
        return true;
    }
}
