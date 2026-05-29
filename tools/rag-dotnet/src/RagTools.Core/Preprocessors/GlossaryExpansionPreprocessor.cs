using RagTools.Core.Config;

namespace RagTools.Core;

/// <summary>
/// Expands multilingual query terms (Polish, German, etc.) to their English equivalents
/// BEFORE embedding.  This bridges the language gap without re-training the model.
///
/// Expansion is applied ONLY on the query path (<see cref="EmbedPurpose.Query"/>).
/// On the ingest path (<see cref="EmbedPurpose.Ingest"/>) the text is returned unchanged
/// so document vectors remain pure representations of the original text.
///
/// Per-collection behavior (ADR-0028 Phase 3):
///   - The mounted YAML glossary (<see cref="RagConfig.GlossaryPath"/>) is loaded once at
///     construction. This is the full set of expansions available.
///   - On each query, <see cref="IConfigSource"/> is asked for the effective payload of the
///     collection resolved by <see cref="RagSession.Collection"/>.
///   - If <see cref="RagConfigPayload.GlossaryTerms"/> is non-empty, it acts as an
///     allow-list of English keys filtering the mounted glossary down to a per-collection
///     subset.
///   - If it is empty (default in STDIO and in HTTP collections without a stored override),
///     the full mounted glossary is used — preserving today's behavior.
/// </summary>
public sealed class GlossaryExpansionPreprocessor(
    RagConfig cfg,
    IConfigSource configSource,
    RagSession session) : IEmbedderPreprocessor
{
    private readonly MultilingualGlossary _mounted = MultilingualGlossary.Load(cfg.GlossaryPath);

    public async Task<string> ProcessAsync(string text, EmbedContext ctx, CancellationToken ct = default)
    {
        // Skip expansion on ingest — document vectors stay pure.
        if (ctx.Purpose == EmbedPurpose.Ingest) return text;

        var payload = await configSource.GetEffectiveAsync(session.Collection, ct);
        var glossary = payload.GlossaryTerms is { Count: > 0 } allow
            ? _mounted.FilterByEnglishKeys(allow)
            : _mounted;
        return glossary.Expand(text);
    }
}
