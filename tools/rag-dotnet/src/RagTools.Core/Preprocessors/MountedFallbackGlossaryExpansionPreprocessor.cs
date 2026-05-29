using RagTools.Core.Config;

namespace RagTools.Core;

/// <summary>
/// Query-time multilingual expansion preprocessor — <b>mounted-fallback</b> variant.
///
/// Per-collection behavior (ADR-0028 Phase 3, Design B):
/// <list type="bullet">
///   <item>On each query, fetches the effective <see cref="RagConfigPayload"/> from
///         <see cref="IConfigSource"/> for the collection resolved by
///         <see cref="RagSession.Collection"/>.</item>
///   <item>If <see cref="RagConfigPayload.GlossaryEntries"/> is non-empty, builds a
///         glossary from those per-collection entries and expands the query with them.</item>
///   <item>If empty (collection has no stored glossary), falls back to the <b>mounted</b>
///         <c>multilingual-glossary.yaml</c> (loaded once from <see cref="RagConfig.GlossaryPath"/>
///         at construction). This is the server-wide common glossary — appropriate for
///         single-org deployments and STDIO mode.</item>
/// </list>
///
/// Ingest path is a no-op (document vectors stay pure representations of source text).
///
/// For true multitenant SaaS where the operator's mounted YAML must <i>not</i> leak into
/// tenant queries, register <see cref="DbOnlyGlossaryExpansionPreprocessor"/> instead.
/// Selection is via the <c>RAG_GLOSSARY_FALLBACK</c> env var in <c>Program.cs</c>.
/// </summary>
public sealed class MountedFallbackGlossaryExpansionPreprocessor(
    RagConfig cfg,
    IConfigSource configSource,
    RagSession session) : IEmbedderPreprocessor
{
    private readonly MultilingualGlossary _mounted = MultilingualGlossary.Load(cfg.GlossaryPath);

    public async Task<string> ProcessAsync(string text, EmbedContext ctx, CancellationToken ct = default)
    {
        if (ctx.Purpose == EmbedPurpose.Ingest) return text;

        var payload = await configSource.GetEffectiveAsync(session.Collection, ct);
        var glossary = payload.GlossaryEntries.Count > 0
            ? MultilingualGlossary.FromEntries(payload.GlossaryEntries)
            : _mounted;
        return glossary.Expand(text);
    }
}
