namespace RagTools.Core;

/// <summary>
/// Scoped service that holds the active Qdrant collection name for the current MCP session.
///
/// Collection resolution order:
///   1. <c>?project=name</c> query parameter in the SSE/HTTP connection URL
///   2. <c>RAG_COLLECTION</c> environment variable
///   3. Default from <c>config.yaml</c> (<see cref="RagConfig.Collection"/>)
///
/// Registered as scoped in DI so each MCP session gets its own instance.
/// Resolved in <c>RagSessionMiddleware</c> which runs before MCP tool dispatch.
/// </summary>
public sealed class RagSession
{
    /// <summary>The resolved collection for this session. Never null or empty.</summary>
    public string Collection { get; private set; }

    public RagSession(RagConfig cfg)
    {
        // Default to config collection; overridden by middleware when request context is available.
        Collection = cfg.Collection;
    }

    /// <summary>Called by middleware to override the collection from the request.</summary>
    public void SetCollection(string collection)
    {
        if (!string.IsNullOrWhiteSpace(collection))
            Collection = collection;
    }
}
