using RagTools.Core;
using Xunit;

namespace RagTools.Tests;

/// <summary>
/// Unit tests for MarkdownChunker.
/// Uses a stub BertTokenCounter (whitespace fallback) — no model required.
/// </summary>
public class MarkdownChunkerTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>Counter that counts words × 1.3 (the fallback formula).</summary>
    private static BertTokenCounter FallbackCounter()
        => BertTokenCounter.FromModelDir("/nonexistent/path");

    /// <summary>
    /// Default chunker with a very low min-token threshold so short test fixtures are not dropped.
    /// Use MinTokenGuardChunker when testing the min-token guard specifically.
    /// </summary>
    private static MarkdownChunker DefaultChunker() => new(
        new ChunkerSection { MaxTokens = 800, MinTokens = 5, OverlapTokens = 80 },
        FallbackCounter());

    private static MarkdownChunker MinTokenGuardChunker(int minTokens = 40) => new(
        new ChunkerSection { MaxTokens = 800, MinTokens = minTokens, OverlapTokens = 80, SplitOnHeadings = [1, 2, 3] },
        FallbackCounter());

    private static MarkdownChunker SmallChunker(int maxTokens = 10, int minTokens = 1, int overlapTokens = 2) => new(
        new ChunkerSection { MaxTokens = maxTokens, MinTokens = minTokens, OverlapTokens = overlapTokens },
        FallbackCounter());

    // ── Basic splitting ──────────────────────────────────────────────────────────

    [Fact]
    public void Chunk_EmptyDocument_ReturnsNoChunks()
    {
        var chunks = DefaultChunker().Chunk("", "empty.md");
        Assert.Empty(chunks);
    }

    [Fact]
    public void Chunk_WhitespaceOnly_ReturnsNoChunks()
    {
        var chunks = DefaultChunker().Chunk("   \n\n\t  ", "ws.md");
        Assert.Empty(chunks);
    }

    [Fact]
    public void Chunk_SingleSection_ReturnsOneChunk()
    {
        var md = "# Title\n\nThis is a paragraph with enough words to pass the min-token guard.";
        var chunks = DefaultChunker().Chunk(md, "doc.md");
        Assert.Single(chunks);
    }

    [Fact]
    public void Chunk_TwoH2Sections_ReturnsTwoChunks()
    {
        var md = """
            ## Section A

            First section body with enough words here to pass the minimum token threshold needed.

            ## Section B

            Second section body with enough words here to pass the minimum token threshold needed.
            """;

        var chunks = DefaultChunker().Chunk(md, "doc.md");
        Assert.Equal(2, chunks.Count);
    }

    [Fact]
    public void Chunk_H1ThenH2_PreservesHierarchy()
    {
        var md = """
            # Top Level

            Intro paragraph with sufficient words here to pass the minimum token threshold.

            ## Subsection

            Body of the subsection with sufficient words to pass the minimum token threshold.
            """;

        var chunks = DefaultChunker().Chunk(md, "doc.md");
        // We expect two sections: the intro under H1, and the H2 section.
        Assert.Equal(2, chunks.Count);
    }

    // ── Breadcrumb construction ──────────────────────────────────────────────────

    [Fact]
    public void Chunk_Breadcrumb_ContainsDocTitle()
    {
        // When an H1 exists, DetectTitle uses it as the doc title.
        var md = "# My Doc\n\nContent with enough words here to pass threshold.";
        var chunks = DefaultChunker().Chunk(md, "path/doc.md");
        Assert.All(chunks, c => Assert.Contains("My Doc", c.Breadcrumb));
    }

    [Fact]
    public void Chunk_Breadcrumb_ContainsHeadingTitle()
    {
        var md = "## Feature A\n\nContent with enough words here to pass the minimum token threshold for test.";
        var chunks = DefaultChunker().Chunk(md, "doc.md");
        var chunk = Assert.Single(chunks);
        Assert.Contains("Feature A", chunk.Breadcrumb);
    }

    [Fact]
    public void Chunk_Breadcrumb_ChainsH1H2WithArrow()
    {
        var md = """
            # Parent

            Intro body text with enough words here to pass minimum token threshold for test.

            ## Child

            Child body text with enough words here to pass minimum token threshold for test.
            """;

        var chunks = DefaultChunker().Chunk(md, "doc.md");
        var childChunk = chunks.Last();
        Assert.Contains(" > ", childChunk.Breadcrumb);
        Assert.Contains("Child", childChunk.Breadcrumb);
    }

    // ── min_tokens guard ────────────────────────────────────────────────────────

    [Fact]
    public void Chunk_SectionBelowMinTokens_IsDropped()
    {
        // MinTokenGuardChunker uses min = 40. A two-word body won't reach it.
        var md = "## Tiny\n\nHi there.";
        var chunks = MinTokenGuardChunker().Chunk(md, "doc.md");
        Assert.Empty(chunks);
    }

    [Fact]
    public void Chunk_SectionAboveMinTokens_IsKept()
    {
        // 1-word body should pass when min = 1.
        var md = "## Section\n\nHello world foo bar.";
        var chunks = DefaultChunker().Chunk(md, "doc.md"); // min = 5
        Assert.Single(chunks);
    }

    // ── Code-fence handling ──────────────────────────────────────────────────────

    [Fact]
    public void Chunk_HeadingInsideCodeFence_IsNotSplit()
    {
        var md = """
            ## Real Section

            Some body text with enough words here to pass minimum token threshold for testing purposes.

            ```markdown
            ## Fake heading inside fence
            ```

            More body text after the fence to ensure we stay in one section boundary.
            """;

        var chunks = DefaultChunker().Chunk(md, "doc.md");
        // Everything should be one section — the fenced heading must NOT trigger a split.
        Assert.Single(chunks);
    }

    // ── Oversized sections → sliding window ─────────────────────────────────────

    [Fact]
    public void Chunk_OversizedSection_IsSplitIntoMultipleChunks()
    {
        // Small chunker: max = 10 tokens, min = 1, overlap = 2.
        // Build a section with many paragraphs that clearly exceed 10 tokens.
        var bigSection = string.Join("\n\n",
            Enumerable.Range(1, 10)
                .Select(i => $"Paragraph {i} contains five distinct words."));

        var md = $"## Big Section\n\n{bigSection}";
        var chunks = SmallChunker(maxTokens: 10, minTokens: 1).Chunk(md, "doc.md");
        Assert.True(chunks.Count > 1);
    }

    [Fact]
    public void Chunk_SlidingWindow_AllChunksRespectMaxTokens()
    {
        // max = 15 tokens, min = 1
        var bigSection = string.Join("\n\n",
            Enumerable.Range(1, 15)
                .Select(i => $"Paragraph {i} has exactly five words total."));

        var md = $"## Section\n\n{bigSection}";
        var counter = FallbackCounter();
        var chunker = new MarkdownChunker(
            new ChunkerSection { MaxTokens = 15, MinTokens = 1, OverlapTokens = 3 },
            counter);

        var chunks = chunker.Chunk(md, "doc.md");
        Assert.All(chunks, c => Assert.True(c.TokenCount <= 15,
            $"Chunk has {c.TokenCount} tokens, exceeds max of 15"));
    }

    [Fact]
    public void Chunk_SlidingWindow_TokenCountMatchesText()
    {
        var bigSection = string.Join("\n\n",
            Enumerable.Range(1, 20)
                .Select(i => $"Paragraph number {i} has several interesting words included here."));

        var md = $"## Big\n\n{bigSection}";
        var counter = FallbackCounter();
        var chunker = new MarkdownChunker(
            new ChunkerSection { MaxTokens = 20, MinTokens = 1, OverlapTokens = 4 },
            counter);

        var chunks = chunker.Chunk(md, "doc.md");
        Assert.All(chunks, c =>
        {
            var expected = counter.Count(c.Text);
            Assert.Equal(expected, c.TokenCount);
        });
    }

    // ── Line number tracking ─────────────────────────────────────────────────────

    [Fact]
    public void Chunk_StartLine_IsPositive()
    {
        var md = "# Doc\n\nContent with enough words here to pass minimum token threshold for test.";
        var chunks = DefaultChunker().Chunk(md, "doc.md");
        Assert.All(chunks, c => Assert.True(c.StartLine >= 1));
    }

    [Fact]
    public void Chunk_EndLine_IsGreaterThanOrEqualToStartLine()
    {
        var md = """
            ## A

            Body A with enough words here to pass minimum token threshold for test.

            ## B

            Body B with enough words here to pass minimum token threshold for test.
            """;

        var chunks = DefaultChunker().Chunk(md, "doc.md");
        Assert.All(chunks, c => Assert.True(c.EndLine >= c.StartLine));
    }

    // ── relPath as fallback title ────────────────────────────────────────────────

    [Fact]
    public void Chunk_NoH1_UsesRelPathAsDocTitle()
    {
        var md = "## Section\n\nContent with enough words here to pass minimum token threshold for test.";
        var chunks = DefaultChunker().Chunk(md, "folder/file.md");
        var chunk = Assert.Single(chunks);
        Assert.StartsWith("folder/file.md", chunk.Breadcrumb);
    }

    // ── SplitOnHeadings configuration ───────────────────────────────────────────

    [Fact]
    public void Chunk_SplitOnHeadingsH1Only_DoesNotSplitOnH2OrH3()
    {
        var chunker = new MarkdownChunker(
            new ChunkerSection { MaxTokens = 800, MinTokens = 1, OverlapTokens = 0, SplitOnHeadings = [1] },
            FallbackCounter());

        var md = """
            # Doc

            intro text

            ## Section A

            section A content

            ### Sub A1

            sub-section content
            """;

        // Only H1 is a boundary; H2 and H3 are treated as body text → single chunk.
        var chunks = chunker.Chunk(md, "test.md");
        Assert.Single(chunks);
    }

    [Fact]
    public void Chunk_SplitOnHeadingsH1H2_DoesNotSplitOnH3()
    {
        var chunker = new MarkdownChunker(
            new ChunkerSection { MaxTokens = 800, MinTokens = 1, OverlapTokens = 0, SplitOnHeadings = [1, 2] },
            FallbackCounter());

        var md = """
            # Doc

            intro

            ## Section A

            section content

            ### Sub A1

            sub content
            """;

        // H1 and H2 are boundaries → chunks: [intro], [Section A content including ### Sub A1]
        var chunks = chunker.Chunk(md, "test.md");
        Assert.Equal(2, chunks.Count);
        Assert.DoesNotContain(chunks, c => c.Breadcrumb.Contains("Sub A1"));
    }

    [Fact]
    public void Chunk_SplitOnHeadings_DefaultBehaviourMatchesH1H2H3()
    {
        var defaultChunker = new MarkdownChunker(
            new ChunkerSection { MaxTokens = 800, MinTokens = 1, OverlapTokens = 0 },
            FallbackCounter());
        var explicitChunker = new MarkdownChunker(
            new ChunkerSection { MaxTokens = 800, MinTokens = 1, OverlapTokens = 0, SplitOnHeadings = [1, 2, 3] },
            FallbackCounter());

        var md = "# Doc\n\nintro\n\n## A\n\ncontent A\n\n### A1\n\ncontent A1";
        var defaultChunks = defaultChunker.Chunk(md, "test.md");
        var explicitChunks = explicitChunker.Chunk(md, "test.md");

        Assert.Equal(defaultChunks.Count, explicitChunks.Count);
    }

    // ── Auto mode (split_on_headings: "auto") ───────────────────────────────────

    private static MarkdownChunker AutoChunker(int minTokens = 1) => new(
        new ChunkerSection { MaxTokens = 800, MinTokens = minTokens, OverlapTokens = 0,
            SplitOnHeadingsRaw = "auto" },
        FallbackCounter());

    [Fact]
    public void AutoMode_H4HeadingsBecomeChunkBoundaries()
    {
        // H4 headings should trigger a new chunk in auto mode.
        var md = """
            ## Section

            Section body with several words to pass the min token threshold.

            #### Detail A

            Detail A has several words to pass the min token threshold here.

            #### Detail B

            Detail B also has several words to pass the min token threshold.
            """;

        var chunks = AutoChunker(minTokens: 1).Chunk(md, "doc.md");
        var breadcrumbs = chunks.Select(c => c.Breadcrumb).ToList();
        Assert.True(breadcrumbs.Any(b => b.Contains("Detail A")), "Detail A should become its own chunk");
        Assert.True(breadcrumbs.Any(b => b.Contains("Detail B")), "Detail B should become its own chunk");
    }

    [Fact]
    public void AutoMode_SmallConsecutiveSectionsAreMerged()
    {
        // Two tiny sections (each < minTokens when alone) should be merged into one chunk.
        var md = """
            ## Alpha

            tiny

            ## Beta

            words
            """;

        // minTokens = 5: each section alone has ~1-2 tokens; merged they reach ~4+
        var chunks = AutoChunker(minTokens: 5).Chunk(md, "doc.md");
        // The two sections should merge into 1 (or 0 if combined is still < min).
        Assert.True(chunks.Count <= 1);
    }

    [Fact]
    public void AutoMode_LargeSectionNotMergedWithSubsequentSmall()
    {
        // A large section followed by a small one: large is emitted, small merged into nothing → dropped.
        var words50 = string.Join(" ", Enumerable.Repeat("word", 50));
        var md = $"""
            ## Big Section

            {words50}

            ## Tiny

            note
            """;

        var chunks = AutoChunker(minTokens: 20).Chunk(md, "doc.md");
        // Big section should be emitted; "Tiny" is dropped (too small, no next chunk to absorb it).
        Assert.Single(chunks);
        Assert.Contains("Big Section", chunks[0].Breadcrumb);
    }

    [Fact]
    public void AutoMode_RecognisesAutoStringCaseInsensitive()
    {
        // "AUTO", "Auto", and "auto" all activate auto mode.
        var md = "## Section\n\n" + string.Join(" ", Enumerable.Repeat("word", 30));
        foreach (var variant in new[] { "auto", "Auto", "AUTO" })
        {
            var chunker = new MarkdownChunker(
                new ChunkerSection { MaxTokens = 800, MinTokens = 1, OverlapTokens = 0,
                    SplitOnHeadingsRaw = variant },
                FallbackCounter());
            var chunks = chunker.Chunk(md, "doc.md");
            Assert.True(chunks.Count >= 1, $"Variant '{variant}' should produce at least one chunk");
        }
    }

    [Fact]
    public void AutoMode_NullRawProducesAutoModeByDefault()
    {
        // When neither SplitOnHeadings nor SplitOnHeadingsRaw is set, IsAuto should be true.
        var section = new ChunkerSection();
        Assert.True(section.IsAuto);
        Assert.Equal([1, 2, 3, 4, 5, 6], section.SplitLevels);
    }

    [Fact]
    public void AutoMode_ExplicitListDisablesAutoMode()
    {
        var section = new ChunkerSection { SplitOnHeadings = [1, 2, 3] };
        Assert.False(section.IsAuto);
        Assert.Equal([1, 2, 3], section.SplitLevels);
    }

    [Fact]
    public void AutoMode_SplitLevelsContainH1ThroughH6()
    {
        var section = new ChunkerSection { SplitOnHeadingsRaw = "auto" };
        Assert.Equal([1, 2, 3, 4, 5, 6], section.SplitLevels);
    }
}
