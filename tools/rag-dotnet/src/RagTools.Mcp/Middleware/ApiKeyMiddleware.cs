namespace RagTools.Mcp.Middleware;

/// <summary>
/// Middleware that protects /ingest/* and /admin/* routes with an API key.
///
/// The key is supplied in the <c>X-Api-Key</c> HTTP header.
/// Configured via the <c>RAG_API_KEY</c> environment variable.
///
/// When <c>RAG_API_KEY</c> is not set, the middleware logs a warning and allows all requests
/// (useful for local development without auth). In production, set the env var.
///
/// Routes not under /ingest/* or /admin/* are not affected (MCP tool calls pass through).
/// </summary>
public sealed class ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger, string? configuredKey = null)
{
    private const string HeaderName = "X-Api-Key";
    public  const string EnvVarName = "RAG_API_KEY";
    // Fallback: read from env if not injected (supports UseMiddleware<T>() without DI overrides).
    private readonly string? _configuredKey = configuredKey ?? Environment.GetEnvironmentVariable(EnvVarName);

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Only guard ingest and admin routes.
        if (!path.StartsWith("/ingest", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // No key configured — allow all (dev mode).
        if (string.IsNullOrEmpty(_configuredKey))
        {
            logger.LogWarning("RAG_API_KEY is not set — {Path} is unauthenticated (dev mode)", path);
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var suppliedKey) ||
            !string.Equals(suppliedKey, _configuredKey, StringComparison.Ordinal))
        {
            logger.LogWarning("Rejected request to {Path} — invalid or missing {Header}", path, HeaderName);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized — supply a valid X-Api-Key header." });
            return;
        }

        await next(context);
    }
}
