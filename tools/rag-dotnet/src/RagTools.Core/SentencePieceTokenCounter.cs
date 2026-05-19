using Microsoft.ML.Tokenizers;

namespace RagTools.Core;

/// <summary>
/// Token counter and encoder backed by Microsoft.ML.Tokenizers.SentencePieceTokenizer.
/// Loads the SentencePiece BPE model (sentencepiece.bpe.model) from the ONNX model directory.
/// This produces correct tokenization for paraphrase-multilingual-MiniLM-L12-v2 which uses
/// XLM-RoBERTa-style SentencePiece tokenization (not BERT WordPiece).
/// </summary>
public sealed class SentencePieceTokenCounter : ITokenCounter, IDisposable
{
    private readonly SentencePieceTokenizer _tokenizer;

    // XLM-RoBERTa special token IDs (same as paraphrase-multilingual-MiniLM-L12-v2)
    public const int BosTokenId = 0;  // <s>
    public const int PadTokenId = 1;  // <pad>
    public const int EosTokenId = 2;  // </s>
    public const int UnkTokenId = 3;  // <unk>

    private static readonly IReadOnlyDictionary<string, int> SpecialTokens =
        new Dictionary<string, int>
        {
            ["<s>"]    = BosTokenId,
            ["<pad>"]  = PadTokenId,
            ["</s>"]   = EosTokenId,
            ["<unk>"]  = UnkTokenId,
            ["<mask>"] = 250001,
        };

    private SentencePieceTokenCounter(SentencePieceTokenizer tokenizer)
    {
        _tokenizer = tokenizer;
    }

    /// <summary>
    /// Creates a token counter from the model directory.
    /// Expects a <c>sentencepiece.bpe.model</c> file in that directory.
    /// </summary>
    public static SentencePieceTokenCounter FromModelDir(string modelDir)
    {
        var modelPath = Path.Combine(modelDir, "sentencepiece.bpe.model");
        if (!File.Exists(modelPath))
            throw new FileNotFoundException(
                $"sentencepiece.bpe.model not found in model directory: {modelDir}. " +
                "Download it from https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2/resolve/main/sentencepiece.bpe.model",
                modelPath);

        using var stream = File.OpenRead(modelPath);
        // addBeginningOfSentence=true, addEndOfSentence=true so <s> and </s> are included
        var tokenizer = SentencePieceTokenizer.Create(
            stream,
            addBeginningOfSentence: true,
            addEndOfSentence: true,
            specialTokens: SpecialTokens);

        return new SentencePieceTokenCounter(tokenizer);
    }

    /// <summary>Count the number of SentencePiece tokens in <paramref name="text"/>.</summary>
    public int CountTokens(string text) => _tokenizer.CountTokens(text);

    /// <summary>Alias for <see cref="CountTokens"/>.</summary>
    public int Count(string text) => CountTokens(text);

    /// <summary>
    /// Encodes <paramref name="text"/> into token IDs, capped at <paramref name="maxLength"/> tokens.
    /// The returned list includes BOS (<s>=0) at position 0 and EOS (</s>=2) at the last position,
    /// using HuggingFace XLM-RoBERTa vocabulary IDs (not native SentencePiece IDs).
    ///
    /// Native SPM:  unk=0,  bos=1, eos=2, regular tokens start at 3
    /// HuggingFace: bos=0, pad=1, eos=2, unk=3, regular tokens start at 4
    /// The mapping: SPM-id 0→3 (unk), SPM-id 1→0 (bos), SPM-id 2→2 (eos), SPM-id n>=3 → n+1
    /// </summary>
    public IReadOnlyList<int> EncodeToIds(string text, int maxLength = 512)
    {
        var nativeIds = _tokenizer.EncodeToIds(text, maxLength, out _, out _);
        // Remap native SPM IDs to HuggingFace XLM-RoBERTa vocabulary IDs so the ONNX model
        // (which was trained with HuggingFace tokenization) sees the correct embedding rows.
        return [.. nativeIds.Select(id => id switch
        {
            0 => 3,     // <unk> native SPM (0) → <unk> HuggingFace (3)
            1 => 0,     // <s>   native SPM (1) → <s>   HuggingFace (0)
            2 => 2,     // </s>  native SPM (2) → </s>  HuggingFace (2)
            _ => id + 1 // regular tokens: native SPM id n → HuggingFace id n+1
        })];
    }

    public void Dispose() { /* SentencePieceTokenizer has no Dispose */ }
}
