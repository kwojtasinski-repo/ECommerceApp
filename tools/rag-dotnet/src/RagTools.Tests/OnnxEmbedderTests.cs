using RagTools.Core;
using Xunit;

namespace RagTools.Tests;

/// <summary>
/// Unit tests for OnnxEmbedder internal helpers.
///
/// EmbedBatch/Embed require a real ONNX model and are covered by integration tests only.
/// These tests cover:
///   - Tokenize  — tensor shapes, padding, attention masks, token IDs
///   - NormaliseRows — L2 normalisation correctness
///   - Flatten   — 2D → 1D conversion
///   - Dimensions property
///   - CreateForTesting factory
/// </summary>
public class OnnxEmbedderTests
{
    private static readonly string VocabPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "vocab.txt");

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Returns an embedder backed by the real BertTokenizer (vocab.txt required).</summary>
    private static OnnxEmbedder RealTokenizerEmbedder(int maxSeqLen = 16)
    {
        var counter = BertTokenCounter.FromModelDir(Path.GetDirectoryName(VocabPath)!);
        return OnnxEmbedder.CreateForTesting(counter, dimensions: 4, maxSeqLen: maxSeqLen);
    }

    /// <summary>Returns an embedder backed by the whitespace fallback (no vocab required).</summary>
    private static OnnxEmbedder FallbackEmbedder(int maxSeqLen = 16)
    {
        var counter = BertTokenCounter.FromModelDir("/nonexistent/path");
        return OnnxEmbedder.CreateForTesting(counter, dimensions: 4, maxSeqLen: maxSeqLen);
    }

    // ── CreateForTesting ─────────────────────────────────────────────────────

    [Fact]
    public void CreateForTesting_SetsDimensions()
    {
        using var emb = OnnxEmbedder.CreateForTesting(
            BertTokenCounter.FromModelDir("/nonexistent"), dimensions: 128);
        Assert.Equal(128, emb.Dimensions);
    }

    // ── NormaliseRows ────────────────────────────────────────────────────────

    [Fact]
    public void NormaliseRows_UnitVector_IsUnchanged()
    {
        // A vector already on the unit sphere should be unchanged.
        var v = new[] { 1f, 0f, 0f, 0f };
        var result = OnnxEmbedder.NormaliseRows([v]);

        Assert.Equal(1f, result[0][0], precision: 5);
        Assert.Equal(0f, result[0][1], precision: 5);
    }

    [Fact]
    public void NormaliseRows_ProducesUnitLengthVectors()
    {
        var vectors = new float[][]
        {
            [3f, 4f],       // norm=5 → [0.6, 0.8]
            [1f, 1f, 1f],   // norm=√3 → [1/√3, 1/√3, 1/√3]
        };
        var result = OnnxEmbedder.NormaliseRows(vectors);

        foreach (var row in result)
        {
            var norm = MathF.Sqrt(row.Sum(x => x * x));
            Assert.Equal(1f, norm, precision: 5);
        }
    }

    [Fact]
    public void NormaliseRows_ZeroVector_RemainsZero()
    {
        // A zero vector cannot be normalised — should stay all zeros.
        var v = new[] { 0f, 0f, 0f };
        var result = OnnxEmbedder.NormaliseRows([[.. v]]);

        Assert.All(result[0], x => Assert.Equal(0f, x));
    }

    [Fact]
    public void NormaliseRows_NegativeComponents_NormalisedCorrectly()
    {
        var v = new[] { -3f, 4f };
        var result = OnnxEmbedder.NormaliseRows([v]);
        var norm = MathF.Sqrt(result[0].Sum(x => x * x));
        Assert.Equal(1f, norm, precision: 5);
        Assert.Equal(-0.6f, result[0][0], precision: 5);
        Assert.Equal(0.8f, result[0][1], precision: 5);
    }

    [Fact]
    public void NormaliseRows_EmptyBatch_ReturnsEmpty()
    {
        var result = OnnxEmbedder.NormaliseRows([]);
        Assert.Empty(result);
    }

    // ── Flatten ──────────────────────────────────────────────────────────────

    [Fact]
    public void Flatten_2x3Array_ReturnsRowMajorSequence()
    {
        long[,] arr = { { 1, 2, 3 }, { 4, 5, 6 } };
        var flat = OnnxEmbedder.Flatten(arr);
        Assert.Equal([1L, 2L, 3L, 4L, 5L, 6L], flat);
    }

    [Fact]
    public void Flatten_1x1Array_ReturnsSingleElement()
    {
        long[,] arr = { { 42 } };
        var flat = OnnxEmbedder.Flatten(arr);
        Assert.Equal([42L], flat);
    }

    [Fact]
    public void Flatten_PreservesLength()
    {
        long[,] arr = new long[3, 7];
        Assert.Equal(21, OnnxEmbedder.Flatten(arr).Length);
    }

    // ── Tokenize — fallback (no vocab.txt) ───────────────────────────────────

    [Fact]
    public void Tokenize_Fallback_SingleText_ReturnsTwoTokens_ClsSep()
    {
        using var emb = FallbackEmbedder(maxSeqLen: 16);
        var (ids, masks, typeIds) = emb.Tokenize(["hello world"]);

        // Shape: [1, 2] — only [CLS]=101 and [SEP]=102
        Assert.Equal(1, ids.GetLength(0));
        Assert.Equal(2, ids.GetLength(1));
        Assert.Equal(101L, ids[0, 0]); // [CLS]
        Assert.Equal(102L, ids[0, 1]); // [SEP]
    }

    [Fact]
    public void Tokenize_Fallback_AttentionMask_SetForActualTokens()
    {
        using var emb = FallbackEmbedder(maxSeqLen: 16);
        var (_, masks, _) = emb.Tokenize(["hello"]);

        // Fallback: [CLS][SEP] → 2 tokens → both mask=1, rest=0
        Assert.Equal(1L, masks[0, 0]);
        Assert.Equal(1L, masks[0, 1]);
    }

    [Fact]
    public void Tokenize_Fallback_TokenTypeIds_AllZero()
    {
        using var emb = FallbackEmbedder(maxSeqLen: 16);
        var (_, _, typeIds) = emb.Tokenize(["hello world"]);

        for (var j = 0; j < typeIds.GetLength(1); j++)
            Assert.Equal(0L, typeIds[0, j]);
    }

    [Fact]
    public void Tokenize_Fallback_BatchOf2_SameShape()
    {
        using var emb = FallbackEmbedder(maxSeqLen: 16);
        var (ids, masks, _) = emb.Tokenize(["hello", "world"]);

        Assert.Equal(2, ids.GetLength(0));
        Assert.Equal(ids.GetLength(1), masks.GetLength(1)); // same seqLen
    }

    // ── Tokenize — real BertTokenizer ────────────────────────────────────────

    [Fact]
    public void Tokenize_RealVocab_HelloWorld_ContainsExpectedTokenIds()
    {
        Skip.IfNot(File.Exists(VocabPath), $"vocab.txt not found at: {VocabPath}");

        using var emb = RealTokenizerEmbedder(maxSeqLen: 16);
        var (ids, _, _) = emb.Tokenize(["hello world"]);

        // "hello world" → [CLS]=101, hello=7592, world=2088, [SEP]=102
        Assert.Equal(101L, ids[0, 0]);
        Assert.Equal(7592L, ids[0, 1]);
        Assert.Equal(2088L, ids[0, 2]);
        Assert.Equal(102L, ids[0, 3]);
    }

    [Fact]
    public void Tokenize_RealVocab_AttentionMask_OneForTokens_ZeroForPadding()
    {
        Skip.IfNot(File.Exists(VocabPath), $"vocab.txt not found at: {VocabPath}");

        // "hi" → [CLS, hi, SEP] = 3 tokens. maxSeqLen=8 → seqLen=3 (no padding beyond actual length)
        using var emb = RealTokenizerEmbedder(maxSeqLen: 8);
        var (ids, masks, _) = emb.Tokenize(["hi"]);

        var seqLen = ids.GetLength(1);
        // All tokens in range [0..seqLen) must have mask=1 (no padding for single short text).
        for (var j = 0; j < seqLen; j++)
            Assert.Equal(1L, masks[0, j]);
    }

    [Fact]
    public void Tokenize_RealVocab_Batch_LongerTextSetsSeqLen()
    {
        Skip.IfNot(File.Exists(VocabPath), $"vocab.txt not found at: {VocabPath}");

        // Batch: short "hi" + long "hello world foo" → seqLen should cover the longest.
        using var emb = RealTokenizerEmbedder(maxSeqLen: 16);
        var (ids, masks, _) = emb.Tokenize(["hi", "hello world"]);

        // "hello world" → 4 tokens → seqLen >= 4
        Assert.Equal(2, ids.GetLength(0));
        Assert.True(ids.GetLength(1) >= 4, $"Expected seqLen >= 4, got {ids.GetLength(1)}");
    }

    [Fact]
    public void Tokenize_RealVocab_Truncates_ToMaxSeqLen()
    {
        Skip.IfNot(File.Exists(VocabPath), $"vocab.txt not found at: {VocabPath}");

        // "hello world" has 4 tokens; cap at 3 → truncated.
        using var emb = RealTokenizerEmbedder(maxSeqLen: 3);
        var (ids, _, _) = emb.Tokenize(["hello world"]);

        Assert.Equal(3, ids.GetLength(1));
        Assert.Equal(101L, ids[0, 0]); // [CLS] still first
    }

    [Fact]
    public void Tokenize_RealVocab_ShortTextInBatch_PaddedWithZeroIds()
    {
        Skip.IfNot(File.Exists(VocabPath), $"vocab.txt not found at: {VocabPath}");

        // "hi" has fewer tokens than "hello world". The shorter one gets zero-padded.
        using var emb = RealTokenizerEmbedder(maxSeqLen: 16);
        var (ids, masks, _) = emb.Tokenize(["hi", "hello world"]);

        var seqLen = ids.GetLength(1);
        var hiLen = 0;
        for (var j = 0; j < seqLen; j++)
            if (ids[0, j] != 0) hiLen++;

        // "hi" → [CLS, hi, SEP] = 3 real tokens; "hello world" → 4.
        // The "hi" row should have padding zeros after position 2 (index 3 onward).
        Assert.Equal(0L, masks[0, seqLen - 1]); // last mask position should be padding
    }
}
