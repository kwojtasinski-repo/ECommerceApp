using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using RagTools.Core.Query;

namespace RagTools.Mcp.Tools;

/// <summary>
/// Pure formatter for <c>QueryDocsCached</c>. Mirrors the Python
/// <c>_derive_source_label</c> / <c>_format_chunks_to_markdown</c> /
/// <c>_tool_query_docs_cached</c> logic in <c>tools/rag/rag_tools.py</c> so the
/// two MCP servers produce byte-equivalent payloads for the same hits.
/// </summary>
internal static class QueryDocsCachedFormatter
{
    private const int MaxChunksPerFile = 5;

    private static readonly Regex AdrIdRegex =
        new(@"\b(?:adr[-\s]?)?(\d{3,4})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NonAlnumRegex =
        new("[^a-z0-9]+", RegexOptions.Compiled);

    private static readonly char[] TrailingSentencePunctuation = ['?', '.', '!'];

    public sealed record CachedPayload(
        string Source,
        string Markdown,
        int FilesCount,
        int ChunksCount,
        string Query,
        string? Bc,
        string NextStep)
    {
        public object ToProjection() => new
        {
            source = Source,
            markdown = Markdown,
            files_count = FilesCount,
            chunks_count = ChunksCount,
            query = Query,
            bc = Bc,
            next_step = NextStep,
        };
    }

    public static CachedPayload Build(
        string question,
        string? bc,
        int topFiles,
        IReadOnlyList<QueryHit> hits,
        DateTime utcNow)
    {
        var byFile = new Dictionary<string, List<QueryHit>>(StringComparer.Ordinal);
        var bestScore = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var h in hits)
        {
            if (!byFile.TryGetValue(h.RelPath, out var list))
            {
                list = new List<QueryHit>();
                byFile[h.RelPath] = list;
            }
            list.Add(h);
            if (!bestScore.TryGetValue(h.RelPath, out var s) || h.Score > s)
                bestScore[h.RelPath] = h.Score;
        }

        var rankedFiles = bestScore
            .OrderByDescending(kv => kv.Value)
            .Take(topFiles)
            .ToList();

        var filesOut = new List<FormattedFile>(rankedFiles.Count);
        var totalChunks = 0;
        foreach (var kv in rankedFiles)
        {
            var top = byFile[kv.Key]
                .OrderByDescending(h => h.Score)
                .Take(MaxChunksPerFile)
                .ToList();
            totalChunks += top.Count;
            filesOut.Add(new FormattedFile(kv.Key, Math.Round(kv.Value, 4), top));
        }

        var source = DeriveSourceLabel(question, bc);
        var markdown = FormatMarkdown(question, bc, filesOut, utcNow);
        var nextStep =
            $"ctx_index(content=<markdown>, source=\"{source}\"); " +
            $"then ctx_search(queries=[...], source=\"{source}\") for recalls.";

        return new CachedPayload(source, markdown, filesOut.Count, totalChunks, question, bc, nextStep);
    }

    public static string DeriveSourceLabel(string question, string? bc)
    {
        var norm = (question ?? string.Empty).Trim().ToLowerInvariant();
        var h8 = ShortHash(norm);
        var m = AdrIdRegex.Match(question ?? string.Empty);
        if (m.Success)
        {
            var adrId = m.Groups[1].Value.PadLeft(4, '0');
            return $"rag-cache-adr{adrId}-{h8}";
        }
        if (!string.IsNullOrWhiteSpace(bc))
            return $"rag-cache-{Slugify(bc!)}-{h8}";
        return $"rag-cache-q-{h8}";
    }

    internal static string Slugify(string text, int maxLen = 30)
    {
        var lower = text.ToLowerInvariant();
        var collapsed = NonAlnumRegex.Replace(lower, "-").Trim('-');
        if (collapsed.Length == 0) return "q";
        if (collapsed.Length > maxLen) collapsed = collapsed[..maxLen].TrimEnd('-');
        return collapsed.Length == 0 ? "q" : collapsed;
    }

    private static string ShortHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var sb = new StringBuilder(8);
        for (var i = 0; i < 4; i++) sb.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    private static string FormatMarkdown(
        string question, string? bc, IReadOnlyList<FormattedFile> files, DateTime utcNow)
    {
        var date = utcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var bcArg = string.IsNullOrEmpty(bc) ? string.Empty : $", bc=\"{bc}\"";
        var topic = (question ?? string.Empty).Trim().TrimEnd(TrailingSentencePunctuation);
        if (topic.Length == 0) topic = "RAG cache";

        var sb = new StringBuilder();
        sb.Append("# ").Append(topic).Append('\n');
        sb.Append('\n');
        sb.Append("> Cached from RAG on ").Append(date)
          .Append(". Source: query_docs_cached(\"").Append(question).Append('"').Append(bcArg).Append(").\n");
        sb.Append("> Refresh: re-run query_docs_cached with the same parameters to overwrite.\n");
        sb.Append('\n');

        foreach (var f in files)
        {
            var title = f.RelPath.Split('/')[^1];
            sb.Append("## ").Append(title).Append('\n');
            sb.Append('\n');
            QueryHit? first = f.Chunks.Count > 0 ? f.Chunks[0] : null;
            if (first is not null)
            {
                sb.Append("**Path**: `").Append(f.RelPath)
                  .Append("#L").Append(first.StartLine)
                  .Append("-L").Append(first.EndLine).Append("`\n");
            }
            else
            {
                sb.Append("**Path**: `").Append(f.RelPath).Append("`\n");
            }

            var crumb = f.Chunks
                .Select(c => c.Breadcrumb)
                .FirstOrDefault(b => !string.IsNullOrEmpty(b));
            if (!string.IsNullOrEmpty(crumb))
            {
                sb.Append("**Breadcrumb**: ").Append(crumb).Append('\n');
            }
            sb.Append('\n');

            foreach (var c in f.Chunks)
            {
                sb.Append(c.Text.TrimEnd()).Append('\n');
                sb.Append('\n');
                sb.Append("---\n");
                sb.Append('\n');
            }
        }

        var result = sb.ToString().TrimEnd();
        return result + "\n";
    }

    internal sealed record FormattedFile(string RelPath, double Score, IReadOnlyList<QueryHit> Chunks);
}
