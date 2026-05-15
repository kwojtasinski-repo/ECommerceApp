using Microsoft.ML.Tokenizers;

namespace RagTools.Core;

/// <summary>
/// Token counter backed by Microsoft.ML.Tokenizers.BertTokenizer.
/// Uses the vocab.txt from the ONNX model directory for accurate WordPiece tokenization.
/// Falls back to whitespace approximation if vocab.txt is not found.
/// </summary>
public sealed class BertTokenCounter
{
    private readonly BertTokenizer? _tokenizer;
    private const double SubwordFactor = 1.3;

    private BertTokenCounter(BertTokenizer? tokenizer)
    {
        _tokenizer = tokenizer;
    }

    /// <summary>
    /// Create a token counter from the model directory.
    /// Expects a <c>vocab.txt</c> file in that directory (standard BERT layout).
    /// Falls back to whitespace approximation if the file is not present.
    /// </summary>
    public static BertTokenCounter FromModelDir(string modelDir)
    {
        var vocabPath = Path.Combine(modelDir, "vocab.txt");
        if (!File.Exists(vocabPath))
            return new BertTokenCounter(null); // fallback mode

        var tokenizer = BertTokenizer.Create(vocabPath, new BertOptions
        {
            LowerCaseBeforeTokenization = true,
        });
        return new BertTokenCounter(tokenizer);
    }

    /// <summary>Count the number of BERT WordPiece tokens in <paramref name="text"/>.</summary>
    public int CountTokens(string text)
    {
        if (_tokenizer is not null)
            return _tokenizer.CountTokens(text);

        // Fallback: whitespace-word count × subword inflation factor.
        var wordCount = text.Split((char[])[], StringSplitOptions.RemoveEmptyEntries).Length;
        return (int)Math.Ceiling(wordCount * SubwordFactor);
    }

    /// <summary>
    /// Encodes <paramref name="text"/> into BERT token IDs, capped at <paramref name="maxLength"/> tokens.
    /// Returns <c>null</c> when running in fallback mode (no vocab.txt available).
    /// The returned list already includes [CLS] (101) as the first token and [SEP] (102) as the last.
    /// </summary>
    public IReadOnlyList<int>? EncodeToIds(string text, int maxLength = 512)
    {
        if (_tokenizer is null) return null;
        var ids = _tokenizer.EncodeToIds(text, maxLength, out string? _, out int _, true, false);
        return ids;
    }

    /// <summary>Alias for <see cref="CountTokens"/> — kept for API compatibility with callers.</summary>
    public int Count(string text) => CountTokens(text);
}
