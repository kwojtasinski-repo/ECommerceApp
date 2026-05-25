namespace RagTools.Core.Primitives;

/// <summary>
/// Inclusive 1-based line range within a source file.
/// Invariant: <c>1 &lt;= StartLine &lt;= EndLine</c>.
/// </summary>
public readonly record struct LineRange
{
    public int StartLine { get; }
    public int EndLine   { get; }

    public LineRange(int startLine, int endLine)
    {
        if (startLine < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(startLine), "StartLine must be >= 1");
        }

        if (endLine < startLine)
        {
            throw new ArgumentOutOfRangeException(nameof(endLine),   "EndLine must be >= StartLine");
        }

        StartLine = startLine;
        EndLine   = endLine;
    }

    public int Length => EndLine - StartLine + 1;

    public bool Contains(int line)            => line >= StartLine && line <= EndLine;
    public bool Overlaps(LineRange other)     => StartLine <= other.EndLine && other.StartLine <= EndLine;

    public override string ToString() => $"L{StartLine}-L{EndLine}";
}
