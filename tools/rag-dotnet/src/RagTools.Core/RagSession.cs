namespace RagTools.Core;

/// <summary>
/// Provides the active Qdrant collection name for the current invocation.
///
/// Collection resolution is delegated to an <see cref="ICollectionResolver"/> so that
/// the session object itself is transport-agnostic:
///   <see cref="FixedCollectionResolver"/> — STDIO mode and tests: always the same collection.
///   HttpCollectionResolver (RagTools.Mcp) — HTTP mode: reads ?project= from the live request
///     via IHttpContextAccessor AsyncLocal, bypassing MCP inner-scope reconstruction issues.
///
/// Registered as Singleton in both transports.
/// </summary>
public sealed class RagSession(ICollectionResolver resolver)
{
    /// <summary>The resolved collection for this invocation. Never null or empty.</summary>
    public string Collection => resolver.GetCollection();
}

