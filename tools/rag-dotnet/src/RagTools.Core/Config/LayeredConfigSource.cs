namespace RagTools.Core.Config;

/// <summary>
/// Default for HTTP mode. Returns mounted defaults merged with a per-collection Qdrant
/// override (override wins per-field; absent override fields keep the default).
///
/// Glossary semantics (see ADR-0028 Phase 3 / P3-3, Design B):
///   • override <see cref="RagConfigPayload.GlossaryEntries"/> is empty → no per-collection
///     entries; the configured preprocessor class decides the fallback
///     (<see cref="MountedFallbackGlossaryExpansionPreprocessor"/> uses the mounted YAML;
///     <see cref="DbOnlyGlossaryExpansionPreprocessor"/> performs no expansion).
///   • override list has entries                                       → use override verbatim
///                                                                      (no merge with mounted)
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
