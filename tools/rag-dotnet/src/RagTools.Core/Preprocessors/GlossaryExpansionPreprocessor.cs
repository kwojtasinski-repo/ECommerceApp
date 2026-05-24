namespace RagTools.Core;

/// <summary>
/// Expands multilingual query terms (Polish, German, etc.) to their English equivalents
/// BEFORE embedding.  This bridges the language gap without re-training the model.
///
/// Expansion is applied ONLY on the query path (<see cref="EmbedPurpose.Query"/>).
/// On the ingest path (<see cref="EmbedPurpose.Ingest"/>) the text is returned unchanged
/// so document vectors remain pure representations of the original text.
/// </summary>
public sealed class GlossaryExpansionPreprocessor(RagConfig cfg) : IEmbedderPreprocessor
{
    private readonly MultilingualGlossary _glossary = MultilingualGlossary.Load(cfg.GlossaryPath);

    public Task<string> ProcessAsync(string text, EmbedContext ctx, CancellationToken ct = default)
    {
        // Skip expansion on ingest — document vectors stay pure.
        if (ctx.Purpose == EmbedPurpose.Ingest)
        {
            return Task.FromResult(text);
        }

        return Task.FromResult(_glossary.Expand(text));
    }
}
