using RagTools.Core;
using RagTools.Core.Config;
using Xunit;

namespace RagTools.Tests;

/// <summary>
/// Unit tests for the built-in preprocessors:
///   - <see cref="MountedFallbackGlossaryExpansionPreprocessor"/> — mounted-fallback variant.
///   - <see cref="DbOnlyGlossaryExpansionPreprocessor"/>           — DB-only variant (no fallback).
///   - <see cref="LengthTruncationPreprocessor"/>                  — word-count hard truncation.
/// </summary>
public class BuiltinPreprocessorTests
{
    // ── MountedFallbackGlossaryExpansionPreprocessor ─────────────────────────

    [Fact]
    public async Task MountedGlossary_SkipsOnIngest()
    {
        var cfg = BuildConfig();
        var pre = new MountedFallbackGlossaryExpansionPreprocessor(cfg, new StubConfigSource(), new RagSession(new FixedCollectionResolver("test")));

        var result = await pre.ProcessAsync("zamówienia", EmbedContext.Ingest);
        Assert.Equal("zamówienia", result);
    }

    [Fact]
    public async Task MountedGlossary_RunsOnQuery_NoGlossaryFile_PassesThrough()
    {
        var cfg = BuildConfig();
        var pre = new MountedFallbackGlossaryExpansionPreprocessor(cfg, new StubConfigSource(), new RagSession(new FixedCollectionResolver("test")));

        var result = await pre.ProcessAsync("orders", EmbedContext.Query);
        Assert.Equal("orders", result);
    }

    [Fact]
    public async Task MountedGlossary_EmptyPayloadEntries_FallsBackToMountedYaml()
    {
        var (cfg, glossaryPath) = BuildConfigWithGlossary([
            ("orders",   new[] { "zamówienia", "bestellungen" }),
            ("products", new[] { "produkty", "produkte" }),
        ]);
        try
        {
            // Empty per-collection entries ⇒ fall back to the full mounted glossary.
            var stub = new StubConfigSource { Payload = new RagConfigPayload { GlossaryEntries = [] } };
            var pre  = new MountedFallbackGlossaryExpansionPreprocessor(cfg, stub, new RagSession(new FixedCollectionResolver("test")));

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
    public async Task MountedGlossary_NonEmptyPayloadEntries_UsesPayloadOnly()
    {
        var (cfg, glossaryPath) = BuildConfigWithGlossary([
            ("orders",   new[] { "zamówienia", "bestellungen" }),
            ("products", new[] { "produkty", "produkte" }),
        ]);
        try
        {
            // Per-collection entries are taken verbatim — mounted YAML must NOT leak in.
            var stub = new StubConfigSource
            {
                Payload = new RagConfigPayload
                {
                    GlossaryEntries = [new GlossaryEntry("orders", ["zamówienia", "bestellungen"])],
                },
            };
            var pre = new MountedFallbackGlossaryExpansionPreprocessor(cfg, stub, new RagSession(new FixedCollectionResolver("test")));

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
    public async Task MountedGlossary_ResolvesCollectionPerCall()
    {
        var (cfg, glossaryPath) = BuildConfigWithGlossary([
            ("orders",   new[] { "zamówienia" }),
            ("products", new[] { "produkty" }),
        ]);
        try
        {
            var stub = new StubConfigSource
            {
                ByCollection =
                {
                    ["col_a"] = new RagConfigPayload
                    {
                        GlossaryEntries = [new GlossaryEntry("orders", ["zamówienia"])],
                    },
                    ["col_b"] = new RagConfigPayload
                    {
                        GlossaryEntries = [new GlossaryEntry("products", ["produkty"])],
                    },
                },
            };

            var preA = new MountedFallbackGlossaryExpansionPreprocessor(cfg, stub, new RagSession(new FixedCollectionResolver("col_a")));
            var preB = new MountedFallbackGlossaryExpansionPreprocessor(cfg, stub, new RagSession(new FixedCollectionResolver("col_b")));

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

    // ── DbOnlyGlossaryExpansionPreprocessor ──────────────────────────────────

    [Fact]
    public async Task DbOnlyGlossary_SkipsOnIngest()
    {
        var pre = new DbOnlyGlossaryExpansionPreprocessor(new StubConfigSource(), new RagSession(new FixedCollectionResolver("test")));
        var result = await pre.ProcessAsync("zamówienia", EmbedContext.Ingest);
        Assert.Equal("zamówienia", result);
    }

    [Fact]
    public async Task DbOnlyGlossary_EmptyPayloadEntries_DoesNotExpand()
    {
        // Even though a mounted YAML exists in this test setup, the DB-only variant
        // must NOT consult it — this is the multitenant isolation guarantee.
        var (_, glossaryPath) = BuildConfigWithGlossary([
            ("orders", new[] { "zamówienia" }),
        ]);
        try
        {
            var stub = new StubConfigSource { Payload = new RagConfigPayload { GlossaryEntries = [] } };
            var pre  = new DbOnlyGlossaryExpansionPreprocessor(stub, new RagSession(new FixedCollectionResolver("test")));

            var result = await pre.ProcessAsync("zamówienia", EmbedContext.Query);
            Assert.Equal("zamówienia", result);
            Assert.DoesNotContain("orders", result);
        }
        finally
        {
            File.Delete(glossaryPath);
        }
    }

    [Fact]
    public async Task DbOnlyGlossary_NonEmptyPayloadEntries_ExpandsFromPayload()
    {
        var stub = new StubConfigSource
        {
            Payload = new RagConfigPayload
            {
                GlossaryEntries = [new GlossaryEntry("orders", ["zamówienia"])],
            },
        };
        var pre = new DbOnlyGlossaryExpansionPreprocessor(stub, new RagSession(new FixedCollectionResolver("test")));

        var result = await pre.ProcessAsync("zamówienia", EmbedContext.Query);
        Assert.Contains("orders", result);
    }

    // ── LengthTruncationPreprocessor ─────────────────────────────────────────

    [Fact]
    public async Task LengthTruncation_ShortText_NotTruncated()
    {
        var cfg = BuildConfig(maxTokens: 10);
        var pre = new LengthTruncationPreprocessor(cfg);

        var input = "one two three four five";
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

    private static RagConfig BuildConfig(int maxTokens = 400)
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
