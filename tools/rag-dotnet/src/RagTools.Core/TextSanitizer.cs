using Microsoft.Extensions.Logging;

namespace RagTools.Core;

/// <summary>
/// Replaces U+FFFD (Unicode replacement character) with '?'.
/// U+FFFD appears when a file saved in a legacy encoding (e.g. Windows-1252)
/// is read as UTF-8 — those bytes become garbage in the Qdrant index unless cleaned.
///
/// Single source of truth — both <c>FileIngestor</c> (CLI) and <c>IngestWorker</c> (HTTP)
/// must call this. Historically HTTP skipped sanitization entirely.
/// </summary>
public static class TextSanitizer
{
    /// <summary>Returns sanitized text. No logging.</summary>
    public static string RemoveReplacementChars(string text) =>
        text.Contains('\uFFFD') ? text.Replace("\uFFFD", "?") : text;

    /// <summary>Returns sanitized text and logs a warning with file path + count when chars were found.</summary>
    public static string RemoveReplacementChars(string text, string relPath, ILogger logger)
    {
        if (!text.Contains('\uFFFD'))
        {
            return text;
        }

        var count = text.Count(c => c == '\uFFFD');
        logger.LogWarning(
            "{RelPath}: {Count} Unicode replacement char(s) (U+FFFD) — source encoding corrupt; replacing with '?'",
            relPath, count);
        return text.Replace("\uFFFD", "?");
    }
}
