namespace RagTools.Core;

/// <summary>
/// Resolves the active Qdrant collection name for the current invocation context.
///
/// Two implementations:
///   <see cref="FixedCollectionResolver"/> — STDIO mode and tests: always returns the same collection.
///   HttpCollectionResolver (in RagTools.Mcp) — HTTP mode: reads ?project= from the current request.
///
/// Registered in DI. <see cref="RagSession"/> proxies this to give tools a named, typed access point.
/// </summary>
public interface ICollectionResolver
{
    string GetCollection();
}
