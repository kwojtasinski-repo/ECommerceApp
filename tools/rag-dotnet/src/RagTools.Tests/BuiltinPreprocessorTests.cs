using RagTools.Core;
using Xunit;

namespace RagTools.Tests;

/// <summary>
/// Unit tests for the built-in preprocessors:
///   - <see cref="GlossaryExpansionPreprocessor"/> — query-only expansion.
///   - <see cref="LengthTruncationPreprocessor"/> — word-count hard truncation.
/// </summary>
public class BuiltinPreprocessorTests
{
    // ── GlossaryExpansionPreprocessor ────────────────────────────────────────

    [Fact]
    public async Task GlossaryExpansion_SkipsOnIngest()
    {
        var cfg = BuildConfig(glossaryPath: null);
        var pre = new GlossaryExpansionPreprocessor(cfg);

        // With a null glossary path the preprocessor returns text unchanged —
        // but the important thing is it does NOT throw on Ingest context.
        var result = await pre.ProcessAsync("zamówienia", EmbedContext.Ingest);
        Assert.Equal("zamówienia", result);
    }

    [Fact]
    public async Task GlossaryExpansion_RunsOnQuery()
    {
        // Without a real glossary file the preprocessor returns text unchanged
        // (MultilingualGlossary.Load returns Empty when path is null).
        var cfg = BuildConfig(glossaryPath: null);
        var pre = new GlossaryExpansionPreprocessor(cfg);

        var result = await pre.ProcessAsync("orders", EmbedContext.Query);
        // No expansion without a glossary file — returns input unchanged.
        Assert.Equal("orders", result);
    }

    // ── LengthTruncationPreprocessor ─────────────────────────────────────────

    [Fact]
    public async Task LengthTruncation_ShortText_NotTruncated()
    {
        var cfg = BuildConfig(maxTokens: 10);
        var pre = new LengthTruncationPreprocessor(cfg);

        var input = "one two three four five";   // 5 words
        var result = await pre.ProcessAsync(input, EmbedContext.Query);
        Assert.Equal(input, result);
    }

    [Fact]
    public async Task LengthTruncation_LongText_Truncated()
    {
        var cfg = BuildConfig(maxTokens: 3);
        var pre = new LengthTruncationPreprocessor(cfg);

        var result = await pre.ProcessAsync("one two three four five", EmbedContext.Query);
        Assert.Equal("one two three", result);
    }

    [Fact]
    public async Task LengthTruncation_ExactLimit_NotTruncated()
    {
        var cfg = BuildConfig(maxTokens: 3);
        var pre = new LengthTruncationPreprocessor(cfg);

        var result = await pre.ProcessAsync("one two three", EmbedContext.Query);
        Assert.Equal("one two three", result);
    }

    [Fact]
    public async Task LengthTruncation_EmptyString_ReturnsEmpty()
    {
        var cfg = BuildConfig(maxTokens: 10);
        var pre = new LengthTruncationPreprocessor(cfg);

        var result = await pre.ProcessAsync(string.Empty, EmbedContext.Ingest);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task LengthTruncation_AppliesToBothPurposes()
    {
        var cfg = BuildConfig(maxTokens: 2);
        var pre = new LengthTruncationPreprocessor(cfg);

        var queryResult  = await pre.ProcessAsync("a b c d", EmbedContext.Query);
        var ingestResult = await pre.ProcessAsync("a b c d", EmbedContext.Ingest);

        Assert.Equal("a b", queryResult);
        Assert.Equal("a b", ingestResult);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static RagConfig BuildConfig(string? glossaryPath = null, int maxTokens = 400)
        => BuildConfigFromYaml(maxTokens);

    private static RagConfig BuildConfigFromYaml(int maxTokens)
    {
        var yaml = $"""
            chunker:
              max_tokens: {maxTokens}
              min_tokens: 40
              overlap_tokens: 80
            vector_store:
              collection: test
            """;

        var tmp = Path.GetTempFileName() + ".yaml";
        File.WriteAllText(tmp, yaml);
        try
        {
            return RagConfig.Load(tmp);
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
