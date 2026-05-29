using RagTools.Core.Config;

namespace RagTools.Core;

/// <summary>
/// Hard-truncates text to a maximum word count before it reaches the embedding model.
/// This makes the existing silent truncation inside <see cref="OnnxEmbedder.Tokenize"/>
/// explicit and configurable — and applies it to ALL providers (including Ollama).
///
/// Uses a simple word split as a token proxy (avoids a SentencePiece dependency here).
///
/// ADR-0028 Phase 3 / P3-3b — per-collection resolution:
///   • Query path (<see cref="EmbedPurpose.Query"/>): fetches per-collection MaxTokens via
///     <see cref="IConfigSource"/>. Falls back to the mounted <see cref="RagConfig"/> default
///     when the per-collection payload has no override (MaxTokens == 0).
///   • Ingest path (<see cref="EmbedPurpose.Ingest"/>): always uses the mounted default —
///     ingest runs in a background worker where the ambient <see cref="RagSession"/> may not
///     reflect the per-batch collection (no HttpContext), and the upstream chunker has already
///     enforced the per-collection MaxTokens. Truncation here is a safety net only.
/// </summary>
public sealed class LengthTruncationPreprocessor(
    RagConfig cfg,
    IConfigSource configSource,
    RagSession session) : IEmbedderPreprocessor
{
    public async Task<string> ProcessAsync(string text, EmbedContext ctx, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var maxWords = cfg.Chunker.MaxTokens;
        if (ctx.Purpose == EmbedPurpose.Query)
        {
            var payload = await configSource.GetEffectiveAsync(session.Collection, ct);
            if (payload.MaxTokens > 0) maxWords = payload.MaxTokens;
        }
        if (maxWords <= 0) return text;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= maxWords) return text;

        return string.Join(' ', words[..maxWords]);
    }
}
