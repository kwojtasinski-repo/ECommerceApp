namespace RagTools.Core;

/// <summary>
/// Document metadata extraction helpers — the single source of truth for how a document's
/// title, headings, or other intrinsic metadata are derived from its raw content.
/// </summary>
/// <remarks>
/// Historically two divergent copies of <c>ExtractTitle</c> existed:
/// <list type="bullet">
///   <item><c>FileIngestor.ExtractTitle</c> (CLI path) — stripped BOM, fell back to <c>relPath</c></item>
///   <item><c>IngestWorker.ExtractTitle</c> (HTTP path) — no BOM strip, fell back to <c>Path.GetFileNameWithoutExtension(relPath)</c></item>
/// </list>
/// The same file ingested through different paths produced different <c>doc_title</c> values,
/// which affected search ranking. This class is the canonical implementation; both paths must
/// delegate here.
/// </remarks>
public static class DocumentMetadata
{
    /// <summary>
    /// Extracts a document title from raw text. Returns the first H1 heading (after stripping
    /// optional BOM and YAML front-matter). If none is found, falls back to <paramref name="relPath"/>
    /// (full path, not just file name) so search results retain enough context to be useful.
    /// </summary>
    public static string ExtractTitle(string text, string relPath)
    {
        // Strip UTF-8 BOM if present. StreamReader with detectEncodingFromByteOrderMarks=true
        // (the .NET default) already strips it, but raw in-memory strings may not.
        text = text.TrimStart('\uFEFF');
        foreach (var line in text.Split('\n'))
        {
            var s = line.Trim();
            if (s.StartsWith("# "))
            {
                return s[2..].Trim();
            }

            if (!string.IsNullOrEmpty(s) && !s.StartsWith('#') && !s.StartsWith("---"))
            {
                break;
            }
        }
        return relPath;
    }
}
