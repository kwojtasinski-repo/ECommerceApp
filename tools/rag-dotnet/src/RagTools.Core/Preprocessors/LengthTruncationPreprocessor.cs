namespace RagTools.Core;

/// <summary>
/// Hard-truncates text to a maximum word count before it reaches the embedding model.
/// This makes the existing silent truncation inside <see cref="OnnxEmbedder.Tokenize"/>
/// explicit and configurable — and applies it to ALL providers (including Ollama).
///
/// Uses a simple word split as a token proxy (avoids a SentencePiece dependency here).
/// The word count limit is derived from <see cref="RagConfig"/>:<c>Chunker.MaxTokens</c>.
///
/// Applies on both query and ingest paths — oversized text is always truncated.
/// </summary>
public sealed class LengthTruncationPreprocessor(RagConfig cfg) : IEmbedderPreprocessor
{
    public Task<string> ProcessAsync(string text, EmbedContext ctx, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Task.FromResult(text);
        }

        var maxWords = cfg.Chunker.MaxTokens;
        if (maxWords <= 0)
        {
            return Task.FromResult(text);
        }

        // Simple word-split proxy for token count.
        // Accurate enough for a hard ceiling — doesn't need SentencePiece here.
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= maxWords)
        {
            return Task.FromResult(text);
        }

        return Task.FromResult(string.Join(' ', words[..maxWords]));
    }
}
