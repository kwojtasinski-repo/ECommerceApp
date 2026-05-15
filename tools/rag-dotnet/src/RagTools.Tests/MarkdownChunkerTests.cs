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
        new ChunkerSection { MaxTokens = 800, MinTokens = minTokens, OverlapTokens = 80 },
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
}
