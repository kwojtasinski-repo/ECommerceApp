namespace RagTools.Core;

/// <summary>
/// Heading-aware markdown chunker. Mirrors the Python chunker.py logic.
/// Splits at heading boundaries, respects code fences, enforces token budget.
/// In auto mode (split_on_headings: "auto") splits at all H1–H6 levels and
/// merges sections below min_tokens into the previous chunk instead of dropping them.
/// </summary>
public sealed class MarkdownChunker
{
    private readonly int _maxTokens;
    private readonly int _overlapTokens;
    private readonly int _minTokens;
    private readonly ITokenCounter _tokenCounter;
    private readonly HashSet<int> _splitLevels;
    private readonly int _maxHeadingLevel;
    private readonly bool _autoMode;

    public MarkdownChunker(ChunkerSection cfg, ITokenCounter tokenCounter)
    {
        _maxTokens = cfg.MaxTokens;
        _overlapTokens = cfg.OverlapTokens;
        _minTokens = cfg.MinTokens;
        _tokenCounter = tokenCounter;
        _autoMode = cfg.IsAuto;
        _splitLevels = cfg.SplitLevels.Count > 0
            ? cfg.SplitLevels.ToHashSet()
            : [1, 2, 3];
        _maxHeadingLevel = _splitLevels.Max();
    }

    public IReadOnlyList<Chunk> Chunk(string text, string relPath)
    {
        var lines = text.Split('\n');
        var docTitle = DetectTitle(lines, relPath);

        // Split into heading-bounded sections.
        var sections = SplitBySections(lines, docTitle);
        var rawChunks = new List<Chunk>();

        foreach (var section in sections)
        {
            // If a section fits, emit as a single chunk.
            if (_tokenCounter.Count(section.Text) <= _maxTokens)
            {
                var t = section.Text.Trim();
                var tc = _tokenCounter.Count(t);
                if (!_autoMode && tc < _minTokens) continue;
                if (_autoMode || tc >= _minTokens)
                    rawChunks.Add(section with { Text = t, TokenCount = tc });
                continue;
            }

            // Section is too large: slide a window over paragraphs.
            foreach (var piece in SlideWindow(section))
            {
                if (!_autoMode && piece.TokenCount < _minTokens) continue;
                rawChunks.Add(piece);
            }
        }

        return _autoMode ? MergeSmallChunks(rawChunks) : rawChunks;
    }

    /// <summary>
    /// Auto-mode post-processor: accumulate consecutive small chunks until >= min_tokens.
    /// Mirrors Python's _merge_small_chunks: starts accumulating from the first chunk and
    /// absorbs subsequent small sections. Once a large section is encountered, the buffer
    /// is emitted and the new section starts a fresh buffer.
    /// </summary>
    private List<Chunk> MergeSmallChunks(List<Chunk> chunks)
    {
        if (chunks.Count == 0) return [];

        var result = new List<Chunk>();
        var buf = chunks[0];

        foreach (var chunk in chunks.Skip(1))
        {
            if (buf.TokenCount >= _minTokens)
            {
                // Buffer is large enough — emit it, start a new buffer.
                result.Add(buf);
                buf = chunk;
            }
            else
            {
                // Buffer is still small — try to absorb this chunk.
                var combinedText = buf.Text + "\n\n" + chunk.Text;
                var combinedTokens = _tokenCounter.Count(combinedText);
                if (combinedTokens <= _maxTokens)
                {
                    buf = buf with { Text = combinedText, TokenCount = combinedTokens };
                }
                else
                {
                    // Combined would overflow — emit buffer if large enough, then start fresh.
                    if (buf.TokenCount >= _minTokens)
                        result.Add(buf);
                    buf = chunk;
                }
            }
        }

        // Final buffer: if too small, merge it BACKWARD into the last emitted chunk
        // (trailing-section fix — prevents the very last small H4/section from being dropped).
        if (buf.TokenCount >= _minTokens)
        {
            result.Add(buf);
        }
        else if (result.Count > 0)
        {
            var last = result[^1];
            var combinedText = last.Text + "\n\n" + buf.Text;
            var combinedTokens = _tokenCounter.Count(combinedText);
            if (combinedTokens <= _maxTokens)
                result[^1] = last with { Text = combinedText, TokenCount = combinedTokens };
            else
                result.Add(buf); // overflow — emit as-is rather than silently drop
        }
        else
        {
            result.Add(buf); // only chunk in document — emit regardless of size
        }

        return result;
    }

    private static string DetectTitle(string[] lines, string relPath)
    {
        foreach (var line in lines)
        {
            var s = line.Trim();
            if (s.StartsWith("# ")) return s[2..].Trim();
            if (!string.IsNullOrEmpty(s) && !s.StartsWith('#') && !s.StartsWith("---"))
                break;
        }
        return relPath;
    }

    private List<Chunk> SplitBySections(string[] lines, string docTitle)
    {
        var sections = new List<Chunk>();
        var headingStack = new string?[_maxHeadingLevel + 1]; // index 1.._maxHeadingLevel
        var currentLines = new List<string>();
        var startLine = 1;
        var inFence = false;

        void Flush(int endLine)
        {
            if (currentLines.Count == 0) return;
            var text = string.Join('\n', currentLines).Trim();
            if (string.IsNullOrWhiteSpace(text)) return;

            var breadcrumb = BuildBreadcrumb(docTitle, headingStack);
            var headingPath = string.Join(" > ", headingStack.Skip(1).Where(h => h is not null));
            sections.Add(new Chunk(text, breadcrumb, headingPath, startLine, endLine, _tokenCounter.Count(text)));
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (trimmed.StartsWith("```"))
                inFence = !inFence;

            if (!inFence && IsHeading(trimmed, out var level, out var title) && _splitLevels.Contains(level))
            {
                Flush(i);
                currentLines.Clear();
                startLine = i + 1;
                headingStack[level] = title;
                // Clear deeper levels.
                for (var j = level + 1; j <= _maxHeadingLevel; j++) headingStack[j] = null;
            }

            currentLines.Add(line);
        }
        Flush(lines.Length);
        return sections;
    }

    private static bool IsHeading(string line, out int level, out string title)
    {
        level = 0; title = string.Empty;
        if (!line.StartsWith('#')) return false;
        var i = 0;
        while (i < line.Length && line[i] == '#') i++;
        if (i >= line.Length || line[i] != ' ') return false;
        level = i;
        title = line[(i + 1)..].Trim();
        return true;
    }

    private static string BuildBreadcrumb(string docTitle, string?[] headingStack)
    {
        var parts = new List<string> { docTitle };
        parts.AddRange(headingStack.Skip(1).Where(h => h is not null)!);
        return string.Join(" > ", parts);
    }

    private IEnumerable<Chunk> SlideWindow(Chunk section)
    {
        var paragraphs = section.Text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var buffer = new List<string>();
        var bufTokens = 0;

        foreach (var para in paragraphs)
        {
            var paraTokens = _tokenCounter.Count(para);
            if (bufTokens + paraTokens > _maxTokens && buffer.Count > 0)
            {
                yield return Emit(section, buffer);
                // Overlap: keep last paragraph(s) that fit within _overlapTokens.
                while (buffer.Count > 0 && bufTokens > _overlapTokens)
                {
                    bufTokens -= _tokenCounter.Count(buffer[0]);
                    buffer.RemoveAt(0);
                }
            }
            buffer.Add(para);
            bufTokens += paraTokens;
        }
        if (buffer.Count > 0)
            yield return Emit(section, buffer);
    }

    private Chunk Emit(Chunk section, List<string> paragraphs)
    {
        var text = string.Join("\n\n", paragraphs).Trim();
        return section with
        {
            Text = text,
            TokenCount = _tokenCounter.Count(text),
        };
    }
}

public sealed record Chunk(
    string Text,
    string Breadcrumb,
    string HeadingPath,
    int StartLine,
    int EndLine,
    int TokenCount);
