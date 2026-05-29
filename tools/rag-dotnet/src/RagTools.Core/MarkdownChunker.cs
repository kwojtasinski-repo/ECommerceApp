namespace RagTools.Core;

/// <summary>
/// Heading-aware markdown chunker. Mirrors the Python chunker.py logic.
/// Splits at heading boundaries, respects code fences, enforces token budget.
/// In auto mode (split_on_headings: "auto") splits at all H1–H6 levels and
/// merges sections below min_tokens into the previous chunk instead of dropping them.
///
/// ADR-0028 Phase 3 / P3-3b: the public <see cref="Chunk(string,string,int?,int?)"/>
/// overload accepts per-call MaxTokens / OverlapTokens overrides resolved from the
/// per-collection IConfigSource in <see cref="DocumentProcessor"/>. When overrides are
/// null (or zero) the mounted defaults from <see cref="ChunkerSection"/> apply.
/// MinTokens stays mounted-only — it is not persisted in <see cref="RagConfigPayload"/>.
/// </summary>
public sealed class MarkdownChunker
{
    private readonly int _defaultMaxTokens;
    private readonly int _defaultOverlapTokens;
    private readonly int _minTokens;
    private readonly ITokenCounter _tokenCounter;
    private readonly HashSet<int> _splitLevels;
    private readonly int _maxHeadingLevel;
    private readonly bool _autoMode;

    public MarkdownChunker(ChunkerSection cfg, ITokenCounter tokenCounter)
    {
        _defaultMaxTokens = cfg.MaxTokens;
        _defaultOverlapTokens = cfg.OverlapTokens;
        _minTokens = cfg.MinTokens;
        _tokenCounter = tokenCounter;
        _autoMode = cfg.IsAuto;
        _splitLevels = cfg.SplitLevels.Count > 0
            ? cfg.SplitLevels.ToHashSet()
            : [1, 2, 3];
        _maxHeadingLevel = _splitLevels.Max();
    }

    /// <summary>Chunk with the mounted defaults. Existing callers / tests use this overload.</summary>
    public IReadOnlyList<Chunk> Chunk(string text, string relPath)
        => Chunk(text, relPath, maxTokensOverride: null, overlapTokensOverride: null);

    /// <summary>
    /// Chunk with optional per-collection overrides. Pass null or 0 to keep the mounted default.
    /// </summary>
    public IReadOnlyList<Chunk> Chunk(string text, string relPath, int? maxTokensOverride, int? overlapTokensOverride)
    {
        var maxTokens     = maxTokensOverride     is > 0 ? maxTokensOverride.Value     : _defaultMaxTokens;
        var overlapTokens = overlapTokensOverride is > 0 ? overlapTokensOverride.Value : _defaultOverlapTokens;

        var lines = text.Split('\n');
        var docTitle = DetectTitle(lines, relPath);

        var sections = SplitBySections(lines, docTitle);
        var rawChunks = new List<Chunk>();

        foreach (var section in sections)
        {
            if (_tokenCounter.Count(section.Text) <= maxTokens)
            {
                var t = section.Text.Trim();
                var tc = _tokenCounter.Count(t);
                if (!_autoMode && tc < _minTokens) continue;
                if (_autoMode || tc >= _minTokens)
                    rawChunks.Add(section with { Text = t, TokenCount = tc });
                continue;
            }

            foreach (var piece in SlideWindow(section, maxTokens, overlapTokens))
            {
                if (!_autoMode && piece.TokenCount < _minTokens) continue;
                rawChunks.Add(piece);
            }
        }

        return _autoMode ? MergeSmallChunks(rawChunks, maxTokens) : rawChunks;
    }

    /// <summary>
    /// Auto-mode post-processor: accumulate consecutive small chunks until >= min_tokens.
    /// </summary>
    private List<Chunk> MergeSmallChunks(List<Chunk> chunks, int maxTokens)
    {
        if (chunks.Count == 0) return [];

        var result = new List<Chunk>();
        var buf = chunks[0];

        foreach (var chunk in chunks.Skip(1))
        {
            if (buf.TokenCount >= _minTokens)
            {
                result.Add(buf);
                buf = chunk;
            }
            else
            {
                var combinedText = buf.Text + "\n\n" + chunk.Text;
                var combinedTokens = _tokenCounter.Count(combinedText);
                if (combinedTokens <= maxTokens)
                {
                    buf = buf with { Text = combinedText, TokenCount = combinedTokens };
                }
                else
                {
                    if (buf.TokenCount >= _minTokens)
                        result.Add(buf);
                    buf = chunk;
                }
            }
        }

        if (buf.TokenCount >= _minTokens)
        {
            result.Add(buf);
        }
        else if (result.Count > 0)
        {
            var last = result[^1];
            var combinedText = last.Text + "\n\n" + buf.Text;
            var combinedTokens = _tokenCounter.Count(combinedText);
            if (combinedTokens <= maxTokens)
                result[^1] = last with { Text = combinedText, TokenCount = combinedTokens };
            else
                result.Add(buf);
        }
        else
        {
            result.Add(buf);
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
        var headingStack = new string?[_maxHeadingLevel + 1];
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

    private IEnumerable<Chunk> SlideWindow(Chunk section, int maxTokens, int overlapTokens)
    {
        var paragraphs = section.Text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var buffer = new List<string>();
        var bufTokens = 0;

        foreach (var para in paragraphs)
        {
            var paraTokens = _tokenCounter.Count(para);
            if (bufTokens + paraTokens > maxTokens && buffer.Count > 0)
            {
                yield return Emit(section, buffer);
                while (buffer.Count > 0 && bufTokens > overlapTokens)
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
