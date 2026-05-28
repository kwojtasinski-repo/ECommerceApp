namespace RagTools.Core.Config;

/// <summary>
/// Returns the mounted <see cref="RagConfigPayload"/> regardless of collection.
///
/// Default for STDIO mode (single user, single collection per process) — Qdrant config-fetch
/// is unnecessary, no network/disk hit beyond startup YAML load. Also serves as the
/// "mounted defaults" leg of <see cref="LayeredConfigSource"/>.
///
/// Thread-safe: payload is immutable, captured once at construction.
/// </summary>
public sealed class FileConfigSource : IConfigSource
{
    private readonly RagConfigPayload _payload;

    /// <summary>
    /// Wraps the mounted <see cref="RagConfig"/> as a <see cref="RagConfigPayload"/>.
    /// Glossary terms are intentionally NOT populated here — the
    /// <c>GlossaryExpansionPreprocessor</c> still loads the full multilingual glossary
    /// directly from <see cref="RagConfig.GlossaryPath"/>. P3-3 wires per-collection
    /// glossary persistence; until then the mounted glossary remains the single source.
    /// </summary>
    public FileConfigSource(RagConfig mounted)
    {
        _payload = RagConfigPayload.From(mounted);
    }

    public ValueTask<RagConfigPayload> GetEffectiveAsync(string collection, CancellationToken ct = default) =>
        new(_payload);

    public void Invalidate(string collection) { /* no cache */ }
}
