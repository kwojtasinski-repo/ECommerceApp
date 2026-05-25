namespace RagTools.Core;

/// <summary>
/// Collection resolver that always returns the same collection.
/// Used in STDIO mode (one collection per process) and in unit/E2E tests.
/// </summary>
public sealed class FixedCollectionResolver(string collection) : ICollectionResolver
{
    public string GetCollection() => collection;
}
