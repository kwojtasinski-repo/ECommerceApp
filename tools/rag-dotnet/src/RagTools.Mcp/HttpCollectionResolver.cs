using Microsoft.AspNetCore.Http;
using RagTools.Core;

namespace RagTools.Mcp;

/// <summary>
/// Resolves the active Qdrant collection from the live HTTP request context.
///
/// Resolution order:
///   1. <c>?project=name</c> query parameter on the MCP HTTP connection URL.
///   2. <c>RAG_COLLECTION</c> environment variable.
///   3. Default from <c>rag-config.yaml</c> (<see cref="RagConfig.Collection"/>).
///
/// Uses <see cref="IHttpContextAccessor"/> which propagates the AsyncLocal context
/// established by ASP.NET Core into MCP tool invocation scopes — so this Singleton
/// correctly reads the per-request collection even when the MCP library creates a child DI scope.
/// </summary>
internal sealed class HttpCollectionResolver(
    IHttpContextAccessor httpContextAccessor,
    RagConfig cfg) : ICollectionResolver
{
    public string GetCollection()
    {
        // 1. ?project= query parameter
        var project = httpContextAccessor.HttpContext?.Request.Query["project"]
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        if (project is not null)
            return project;

        // 2. Environment variable
        var envCol = Environment.GetEnvironmentVariable("RAG_COLLECTION");
        if (!string.IsNullOrWhiteSpace(envCol))
            return envCol;

        // 3. Config default
        return cfg.Collection;
    }
}
