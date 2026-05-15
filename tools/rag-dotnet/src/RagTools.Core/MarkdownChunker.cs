namespace RagTools.Core;

/// <summary>
/// Heading-aware markdown chunker. Mirrors the Python chunker.py logic.
/// Splits at H1/H2/H3 boundaries, respects code fences, enforces token budget.
/// </summary>
public sealed class MarkdownChunker
{
    private readonly int _maxTokens;
    private readonly int _overlapTokens;
    private readonly int _minTokens;
    private readonly BertTokenCounter _tokenCounter;

    public MarkdownChunker(ChunkerSection cfg, BertTokenCounter tokenCounter)
    {
        _maxTokens = cfg.MaxTokens;
        _overlapTokens = cfg.OverlapTokens;
        _minTokens = cfg.MinTokens;
        _tokenCounter = tokenCounter;
    }

    public IReadOnlyList<Chunk> Chunk(string text, string relPath)
    {
        var lines = text.Split('\n');
        var docTitle = DetectTitle(lines, relPath);

        // Split into heading-bounded sections.
        var sections = SplitBySections(lines, docTitle);
        var chunks = new List<Chunk>();

        foreach (var section in sections)
        {
            // If a section fits, emit as a single chunk.
            if (_tokenCounter.Count(section.Text) <= _maxTokens)
            {
                var t = section.Text.Trim();
                if (_tokenCounter.Count(t) >= _minTokens)
                    chunks.Add(section with { Text = t });
                continue;
            }

            // Section is too large: slide a window over paragraphs.
            chunks.AddRange(SlideWindow(section));
        }

        return chunks;
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
        var headingStack = new string?[4]; // index 1..3 for H1..H3
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

            if (!inFence && IsHeading(trimmed, out var level, out var title) && level <= 3)
            {
                Flush(i);
                currentLines.Clear();
                startLine = i + 1;
                headingStack[level] = title;
                // Clear deeper levels.
                for (var j = level + 1; j <= 3; j++) headingStack[j] = null;
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
