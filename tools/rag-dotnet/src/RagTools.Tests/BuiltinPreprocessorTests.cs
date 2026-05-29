using RagTools.Core;
using RagTools.Core.Config;
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
        var pre = new GlossaryExpansionPreprocessor(cfg, new StubConfigSource(), new RagSession(new FixedCollectionResolver("test")));

        // With a null glossary path the preprocessor returns text unchanged —
        // but the important thing is it does NOT throw on Ingest context, and IConfigSource
        // must not be consulted on the ingest path.
        var result = await pre.ProcessAsync("zamówienia", EmbedContext.Ingest);
        Assert.Equal("zamówienia", result);
    }

    [Fact]
    public async Task GlossaryExpansion_RunsOnQuery()
    {
        // Without a real glossary file the preprocessor returns text unchanged
        // (MultilingualGlossary.Load returns Empty when path is null).
        var cfg = BuildConfig(glossaryPath: null);
        var pre = new GlossaryExpansionPreprocessor(cfg, new StubConfigSource(), new RagSession(new FixedCollectionResolver("test")));

        var result = await pre.ProcessAsync("orders", EmbedContext.Query);
        // No expansion without a glossary file — returns input unchanged.
        Assert.Equal("orders", result);
    }

    [Fact]
    public async Task GlossaryExpansion_EmptyGlossaryTerms_UsesFullMountedGlossary()
    {
        var (cfg, glossaryPath) = BuildConfigWithGlossary([
            ("orders",   new[] { "zamówienia", "bestellungen" }),
            ("products", new[] { "produkty", "produkte" }),
        ]);
        try
        {
            // Empty GlossaryTerms ⇒ full mounted glossary, so both PL words expand.
            var stub = new StubConfigSource { Payload = new RagConfigPayload { GlossaryTerms = [] } };
            var pre  = new GlossaryExpansionPreprocessor(cfg, stub, new RagSession(new FixedCollectionResolver("test")));

            var result = await pre.ProcessAsync("zamówienia produkty", EmbedContext.Query);
            Assert.Contains("orders", result);
            Assert.Contains("products", result);
        }
        finally
        {
            File.Delete(glossaryPath);
        }
    }

    [Fact]
    public async Task GlossaryExpansion_NonEmptyGlossaryTerms_FiltersMountedGlossary()
    {
        var (cfg, glossaryPath) = BuildConfigWithGlossary([
            ("orders",   new[] { "zamówienia", "bestellungen" }),
            ("products", new[] { "produkty", "produkte" }),
        ]);
        try
        {
            // Allow-list only contains "orders" — "products" must be filtered out.
            var stub = new StubConfigSource { Payload = new RagConfigPayload { GlossaryTerms = ["orders"] } };
            var pre  = new GlossaryExpansionPreprocessor(cfg, stub, new RagSession(new FixedCollectionResolver("test")));

            var result = await pre.ProcessAsync("zamówienia produkty", EmbedContext.Query);
            Assert.Contains("orders", result);
            Assert.DoesNotContain("products", result);
        }
        finally
        {
            File.Delete(glossaryPath);
        }
    }

    [Fact]
    public async Task GlossaryExpansion_ResolvesCollectionPerCall()
    {
        var (cfg, glossaryPath) = BuildConfigWithGlossary([
            ("orders",   new[] { "zamówienia" }),
            ("products", new[] { "produkty" }),
        ]);
        try
        {
            // Different per-collection payloads — preprocessor must look them up by session.Collection.
            var stub = new StubConfigSource
            {
                ByCollection =
                {
                    ["col_a"] = new RagConfigPayload { GlossaryTerms = ["orders"] },
                    ["col_b"] = new RagConfigPayload { GlossaryTerms = ["products"] },
                },
            };

            var preA = new GlossaryExpansionPreprocessor(cfg, stub, new RagSession(new FixedCollectionResolver("col_a")));
            var preB = new GlossaryExpansionPreprocessor(cfg, stub, new RagSession(new FixedCollectionResolver("col_b")));

            var resultA = await preA.ProcessAsync("zamówienia produkty", EmbedContext.Query);
            var resultB = await preB.ProcessAsync("zamówienia produkty", EmbedContext.Query);

            Assert.Contains("orders", resultA);
            Assert.DoesNotContain("products", resultA);
            Assert.Contains("products", resultB);
            Assert.DoesNotContain("orders", resultB);
        }
        finally
        {
            File.Delete(glossaryPath);
        }
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

    private sealed class StubConfigSource : IConfigSource
    {
        public RagConfigPayload Payload { get; set; } = new();
        public Dictionary<string, RagConfigPayload> ByCollection { get; } = new();

        public ValueTask<RagConfigPayload> GetEffectiveAsync(string collection, CancellationToken ct = default)
            => ValueTask.FromResult(ByCollection.TryGetValue(collection, out var p) ? p : Payload);

        public void Invalidate(string collection) { }
    }

    private static (RagConfig Cfg, string GlossaryPath) BuildConfigWithGlossary(
        (string English, string[] Patterns)[] entries)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("entries:");
        foreach (var (en, patterns) in entries)
        {
            sb.AppendLine($"  - english: {en}");
            sb.AppendLine("    patterns:");
            foreach (var p in patterns)
                sb.AppendLine($"      - {p}");
        }
        var glossaryPath = Path.GetTempFileName() + ".yaml";
        File.WriteAllText(glossaryPath, sb.ToString());

        var cfgYaml = $"""
            chunker:
              max_tokens: 400
              min_tokens: 40
              overlap_tokens: 80
            vector_store:
              collection: test
            config_files:
              multilingual_glossary: {glossaryPath.Replace("\\", "/")}
            """;
        var cfgPath = Path.GetTempFileName() + ".yaml";
        File.WriteAllText(cfgPath, cfgYaml);
        try { return (RagConfig.Load(cfgPath), glossaryPath); }
        finally { File.Delete(cfgPath); }
    }

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
