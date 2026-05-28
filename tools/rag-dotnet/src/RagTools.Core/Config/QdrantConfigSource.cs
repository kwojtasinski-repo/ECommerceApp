namespace RagTools.Core.Config;

/// <summary>
/// Pure Qdrant fetch with no mounted fallback. If the collection has never been ingested
/// (no stored config point), throws <see cref="InvalidOperationException"/>.
///
/// Edge case for explicit <c>RAG_CONFIG_SOURCE=qdrant</c> in tests / pure remote setups
/// where the server intentionally has no mounted config. For normal HTTP multi-tenant
/// operation prefer <see cref="LayeredConfigSource"/>.
/// </summary>
public sealed class QdrantConfigSource(IDocumentStore store) : IConfigSource
{
    public async ValueTask<RagConfigPayload> GetEffectiveAsync(string collection, CancellationToken ct = default)
    {
        var stored = await store.FetchConfigAsync(collection, ct);
        return stored
            ?? throw new InvalidOperationException(
                $"No config stored for collection '{collection}' and RAG_CONFIG_SOURCE=qdrant disallows mounted fallback. " +
                "Ingest the collection first or switch to RAG_CONFIG_SOURCE=layered.");
    }

    public void Invalidate(string collection) { /* delegated to CachedDocumentStore */ }
}
