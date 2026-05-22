using RagTools.Core;

namespace RagTools.Mcp.Middleware;

/// <summary>
/// Middleware that resolves the active RAG collection for each session from the
/// <c>?project=name</c> query parameter and injects it into <see cref="RagSession"/>.
///
/// Runs before MCP tool dispatch. The resolved <see cref="RagSession"/> is then
/// available to tools via DI (scoped per request/session).
///
/// Example:
///   mcp.json: "url": "http://localhost:3001/?project=ecommerceapp"
///   → RagSession.Collection = "ecommerceapp"
/// </summary>
public sealed class RagSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, RagSession session)
    {
        if (context.Request.Query.TryGetValue("project", out var project) &&
            !string.IsNullOrWhiteSpace(project))
        {
            session.SetCollection(project!);
        }

        await next(context);
    }
}
