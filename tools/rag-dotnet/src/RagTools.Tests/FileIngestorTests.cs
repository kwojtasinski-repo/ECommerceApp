using RagTools.Core;
using Xunit;

/// <summary>
/// Unit tests for <see cref="FileIngestor.ExtractTitle"/>.
/// </summary>
public sealed class FileIngestorTests
{
    // ── Happy-path ────────────────────────────────────────────────────────────

    [Fact]
    public void ExtractTitle_H1AtStart_ReturnsTitle()
    {
        var text = "# My Title\n\n## Status\n\nAccepted";
        Assert.Equal("My Title", FileIngestor.ExtractTitle(text, "some/path.md"));
    }

    [Fact]
    public void ExtractTitle_H1WithAdrPrefix_ReturnsBareTitle()
    {
        var text = "# ADR-0001: Project Overview\n\n## Status\n\nAccepted";
        // ADR prefix is not stripped by ExtractTitle — it returns everything after "# "
        Assert.Equal("ADR-0001: Project Overview", FileIngestor.ExtractTitle(text, "docs/adr/0001/0001-overview.md"));
    }

    [Fact]
    public void ExtractTitle_NoH1_FallsBackToRelPath()
    {
        var text = "Some prose text without a heading";
        Assert.Equal("docs/adr/0001/0001-overview.md", FileIngestor.ExtractTitle(text, "docs/adr/0001/0001-overview.md"));
    }

    [Fact]
    public void ExtractTitle_EmptyText_FallsBackToRelPath()
    {
        Assert.Equal("foo.md", FileIngestor.ExtractTitle("", "foo.md"));
    }

    // ── UTF-8 BOM handling ────────────────────────────────────────────────────

    [Fact]
    public void ExtractTitle_WithBomPrefix_ReturnsTitle()
    {
        // Files saved with UTF-8 BOM (U+FEFF as first character).
        // File.ReadAllTextAsync with detectEncodingFromByteOrderMarks=true strips the BOM,
        // but raw in-memory strings may include it.  ExtractTitle must handle both.
        var text = "\uFEFF# ADR-0001: Project Overview\n\n## Status\n\nAccepted";
        Assert.Equal("ADR-0001: Project Overview", FileIngestor.ExtractTitle(text, "docs/adr/0001/0001-overview.md"));
    }

    [Fact]
    public void ExtractTitle_BomOnlyFile_FallsBackToRelPath()
    {
        // Edge case: only BOM, no real content.
        Assert.Equal("foo.md", FileIngestor.ExtractTitle("\uFEFF", "foo.md"));
    }

    [Fact]
    public void ExtractTitle_BomBeforeNonH1_FallsBackToRelPath()
    {
        var text = "\uFEFFSome prose without a heading";
        Assert.Equal("foo.md", FileIngestor.ExtractTitle(text, "foo.md"));
    }
}
