namespace RagTools.Core.Config;

/// <summary>
/// Default for HTTP mode. Returns mounted defaults merged with a per-collection Qdrant
/// override (override wins per-field; absent override fields keep the default).
///
/// Glossary semantics (see ADR-0028 Amendment 004):
///   • override <see cref="RagConfigPayload.GlossaryTerms"/> is null   → keep mounted glossary
///   • override list is empty (explicit opt-out)                       → no expansion
///   • override list has entries                                       → use override, no merge
///
/// Caching is the responsibility of <see cref="CachingConfigSource"/> (the decorator). This
/// class always hits <see cref="IDocumentStore.FetchConfigAsync"/> — which is itself cached
/// by <see cref="CachedDocumentStore"/> downstream, so the per-query Qdrant cost is one
/// in-memory dictionary lookup.
/// </summary>
public sealed class LayeredConfigSource(FileConfigSource defaults, IDocumentStore store) : IConfigSource
{
    public async ValueTask<RagConfigPayload> GetEffectiveAsync(string collection, CancellationToken ct = default)
    {
        var defaultPayload = await defaults.GetEffectiveAsync(collection, ct);
        var overridePayload = await store.FetchConfigAsync(collection, ct);
        return overridePayload is null ? defaultPayload : defaultPayload.Merge(overridePayload);
    }

    public void Invalidate(string collection) { /* delegated to CachedDocumentStore */ }
}
