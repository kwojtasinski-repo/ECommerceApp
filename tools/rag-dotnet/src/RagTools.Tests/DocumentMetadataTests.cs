using RagTools.Core;

namespace RagTools.Tests;

/// <summary>
/// Tests for <see cref="DocumentMetadata.ExtractTitle"/>.
/// This is the canonical implementation; both <c>FileIngestor</c> (CLI) and
/// <c>IngestWorker</c> (HTTP) must delegate here so the same file always produces
/// the same <c>doc_title</c> regardless of ingest path.
/// </summary>
public sealed class DocumentMetadataTests
{
    [Fact]
    public void ExtractTitle_H1AtStart_ReturnsTitle()
    {
        var text = "# My Title\n\nbody";
        Assert.Equal("My Title", DocumentMetadata.ExtractTitle(text, "some/path.md"));
    }

    [Fact]
    public void ExtractTitle_H1WithAdrPrefix_ReturnsBareTitle()
    {
        var text = "# ADR-0001: Project Overview\n\nbody";
        Assert.Equal("ADR-0001: Project Overview", DocumentMetadata.ExtractTitle(text, "docs/adr/0001/0001-overview.md"));
    }

    [Fact]
    public void ExtractTitle_NoH1_FallsBackToFullRelPath()
    {
        // Falls back to FULL relPath (not just file name) so the title carries directory context.
        // This is the canonical behavior — IngestWorker previously used GetFileNameWithoutExtension
        // and that was a bug.
        var text = "just body text\nno heading here";
        Assert.Equal("docs/adr/0001/0001-overview.md",
                     DocumentMetadata.ExtractTitle(text, "docs/adr/0001/0001-overview.md"));
    }

    [Fact]
    public void ExtractTitle_EmptyText_FallsBackToRelPath()
    {
        Assert.Equal("foo.md", DocumentMetadata.ExtractTitle("", "foo.md"));
    }

    [Fact]
    public void ExtractTitle_WithBomPrefix_ReturnsTitle()
    {
        var text = "\uFEFF# ADR-0001: Project Overview\n\nbody";
        Assert.Equal("ADR-0001: Project Overview",
                     DocumentMetadata.ExtractTitle(text, "docs/adr/0001/0001-overview.md"));
    }

    [Fact]
    public void ExtractTitle_BomOnlyFile_FallsBackToRelPath()
    {
        Assert.Equal("foo.md", DocumentMetadata.ExtractTitle("\uFEFF", "foo.md"));
    }

    [Fact]
    public void ExtractTitle_BomBeforeNonH1_FallsBackToRelPath()
    {
        var text = "\uFEFFjust plain text\n";
        Assert.Equal("foo.md", DocumentMetadata.ExtractTitle(text, "foo.md"));
    }

    [Fact]
    public void ExtractTitle_HtmlCommentThenH1_StopsBeforeH1()
    {
        // Body text before an H1 means the H1 is not "the title". Fall back to relPath.
        var text = "Some intro paragraph.\n\n# Late Heading\n";
        Assert.Equal("x.md", DocumentMetadata.ExtractTitle(text, "x.md"));
    }

    [Fact]
    public void ExtractTitle_NestedHeadingFirst_FallsBackToRelPath()
    {
        // Only `# ` (H1) wins. `## ` is a sub-heading.
        var text = "## Subheading\nbody";
        // ## starts with '#' so the loop continues skipping, never finds H1 → falls back.
        Assert.Equal("x.md", DocumentMetadata.ExtractTitle(text, "x.md"));
    }

    [Fact]
    public void ExtractTitle_BackslashRelPath_PreservedAsIs()
    {
        // ExtractTitle does NOT normalize separators — that's the caller's job.
        // The bug was IngestWorker using GetFileNameWithoutExtension; here we just return relPath.
        var text = "no heading";
        Assert.Equal(@"docs\adr\file.md", DocumentMetadata.ExtractTitle(text, @"docs\adr\file.md"));
    }
}
