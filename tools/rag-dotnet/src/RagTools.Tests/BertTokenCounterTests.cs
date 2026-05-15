using RagTools.Core;
using Xunit;

namespace RagTools.Tests;

/// <summary>
/// Tests for BertTokenCounter.
/// The bundled TestData/vocab.txt (bert-base-uncased, 30,522 tokens) is used for all
/// real-tokenizer tests so they always run — no external model download required.
///
/// Expected token counts were generated from:
///   BertTokenizer.Create(vocabPath, new BertOptions { LowerCaseBeforeTokenization = true })
/// </summary>
public class BertTokenCounterTests
{
    /// <summary>Path to vocab.txt copied to the output directory by the .csproj.</summary>
    private static string VocabPath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "vocab.txt");

    // ── Fallback (whitespace approximation) ─────────────────────────────────────

    [Fact]
    public void FromModelDir_NonExistentDir_ReturnsFallbackCounter()
    {
        // Arrange
        var counter = BertTokenCounter.FromModelDir("/nonexistent/path");

        // Act — must not throw
        var count = counter.CountTokens("hello world");

        // Assert — fallback returns a positive integer
        Assert.True(count > 0);
    }

    [Fact]
    public void CountTokens_EmptyString_ReturnsZero()
    {
        var counter = BertTokenCounter.FromModelDir("/nonexistent/path");
        Assert.Equal(0, counter.CountTokens(""));
    }

    [Fact]
    public void CountTokens_SingleWord_ReturnsAtLeastOne()
    {
        var counter = BertTokenCounter.FromModelDir("/nonexistent/path");
        Assert.True(counter.CountTokens("hello") >= 1);
    }

    [Fact]
    public void CountTokens_LongerText_IncreasesWith_MoreWords()
    {
        var counter = BertTokenCounter.FromModelDir("/nonexistent/path");

        var shortCount = counter.CountTokens("hello world");
        var longCount = counter.CountTokens("hello world foo bar baz qux");

        Assert.True(longCount > shortCount);
    }

    [Fact]
    public void Count_IsAliasFor_CountTokens()
    {
        var counter = BertTokenCounter.FromModelDir("/nonexistent/path");
        var text = "The quick brown fox jumps over the lazy dog";

        Assert.Equal(counter.CountTokens(text), counter.Count(text));
    }

    [Fact]
    public void CountTokens_WhitespaceOnly_ReturnsZero()
    {
        var counter = BertTokenCounter.FromModelDir("/nonexistent/path");
        Assert.Equal(0, counter.CountTokens("   \t\n  "));
    }

    [Fact]
    public void CountTokens_FallbackAppliesSubwordFactor()
    {
        // 10 words × 1.3 = 13.0, ceiling → 13
        var counter = BertTokenCounter.FromModelDir("/nonexistent/path");
        var tenWords = "one two three four five six seven eight nine ten";
        var count = counter.CountTokens(tenWords);
        Assert.Equal(13, count);
    }

    // ── Real BertTokenizer path ──────────────────────────────────────────────────
    // These tests use the bundled vocab.txt (bert-base-uncased) and always run.
    // Expected values computed by running the same BertTokenizer.Create() call.

    [Fact]
    public void CountTokens_WithRealVocab_CommonEnglishSentence_ReturnsExactCount()
    {
        // "The quick brown fox jumps over the lazy dog today" → 10 tokens
        var counter = BertTokenCounter.FromModelDir(
            Path.GetDirectoryName(VocabPath)!);

        Assert.Equal(10, counter.CountTokens(
            "The quick brown fox jumps over the lazy dog today"));
    }

    [Fact]
    public void CountTokens_WithRealVocab_HelloWorld_ReturnsTwoTokens()
    {
        var counter = BertTokenCounter.FromModelDir(
            Path.GetDirectoryName(VocabPath)!);

        Assert.Equal(2, counter.CountTokens("hello world"));
    }

    [Fact]
    public void CountTokens_WithRealVocab_SingleKnownWord_ReturnsOne()
    {
        var counter = BertTokenCounter.FromModelDir(
            Path.GetDirectoryName(VocabPath)!);

        Assert.Equal(1, counter.CountTokens("hello"));
    }

    [Fact]
    public void CountTokens_WithRealVocab_TechnicalCamelCaseWord_SplitsIntoSubwords()
    {
        // "CreateCollectionAsync" → 6 BERT WordPiece tokens
        // (BERT lower-cases and then splits compound words into known sub-pieces)
        var counter = BertTokenCounter.FromModelDir(
            Path.GetDirectoryName(VocabPath)!);

        Assert.Equal(6, counter.CountTokens("CreateCollectionAsync"));
    }

    [Fact]
    public void CountTokens_WithRealVocab_MoreTokensThanFallback_ForTechnicalText()
    {
        // Real WordPiece tokenization produces more tokens for technical compound
        // words than the whitespace-word estimate does.
        var fallback = BertTokenCounter.FromModelDir("/nonexistent/path");
        var real = BertTokenCounter.FromModelDir(Path.GetDirectoryName(VocabPath)!);

        var text = "CreateCollectionAsync";
        // Fallback: 1 word × 1.3 → 2; Real: 6 subwords.
        Assert.True(real.CountTokens(text) > fallback.CountTokens(text));
    }

    [Fact]
    public void EncodeToIds_WithoutVocab_ReturnsNull()
    {
        var counter = BertTokenCounter.FromModelDir("/nonexistent/path");
        Assert.Null(counter.EncodeToIds("hello world"));
    }

    [SkippableFact]
    public void EncodeToIds_WithRealVocab_HelloWorld_ReturnsExpectedIds()
    {
        Skip.IfNot(File.Exists(VocabPath), $"vocab.txt not found at: {VocabPath}");
        var counter = BertTokenCounter.FromModelDir(Path.GetDirectoryName(VocabPath)!);

        var ids = counter.EncodeToIds("hello world");

        Assert.NotNull(ids);
        // [CLS]=101, hello=7592, world=2088, [SEP]=102
        Assert.Equal([101, 7592, 2088, 102], ids);
    }

    [SkippableFact]
    public void EncodeToIds_WithRealVocab_TruncatesToMaxLength()
    {
        Skip.IfNot(File.Exists(VocabPath), $"vocab.txt not found at: {VocabPath}");
        var counter = BertTokenCounter.FromModelDir(Path.GetDirectoryName(VocabPath)!);

        // "hello world" has 4 tokens including [CLS]/[SEP]; cap at 3
        var ids = counter.EncodeToIds("hello world", maxLength: 3);

        Assert.NotNull(ids);
        Assert.Equal(3, ids!.Count);
        Assert.Equal(101, ids[0]); // [CLS] always first
    }
}
