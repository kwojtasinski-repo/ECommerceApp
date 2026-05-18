using System.ComponentModel;
using ModelContextProtocol.Server;
using RagTools.Core;

namespace RagTools.Mcp.Tools;

/// <summary>
/// MCP tools exposed to Copilot. Mirrors the Python mcp_server.py tools:
///   query_docs      — semantic search across all indexed documentation
///   get_adr_history — fetch all chunks for a specific ADR ID
///   list_adrs       — list all distinct ADR IDs in the index
/// </summary>
[McpServerToolType]
public sealed class RagTools(OnnxEmbedder embedder, QdrantStore store, RagConfig cfg)
{
    [McpServerTool, Description(
        "Semantically search the documentation index. " +
        "Returns the top-k most relevant chunks with breadcrumbs and scores. " +
        "Use bc (bounded context name) or doc_kind to narrow results.")]
    public async Task<string> QueryDocs(
        [Description("The search question or topic.")] string question,
        [Description("Optional bounded context filter — matched against doc_kind field (e.g. 'adr_main').")] string? bc = null,
        [Description("Maximum number of results to return (default: 5, max: 20).")] int topK = 5,
        CancellationToken cancellationToken = default)
    {
        topK = Math.Clamp(topK, 1, 20);

        var queryVec = embedder.Embed(question);
        var hits = await store.SearchAsync(
            queryVec,
            topK,
            cfg.Query.ScoreThreshold,
            docKindFilter: bc,
            cancellationToken: cancellationToken);

        if (hits.Count == 0)
            return "No results found. Consider re-running the ingest script or broadening your query.";

        return string.Join("\n\n---\n\n", hits.Select((h, i) =>
            $"[{i + 1}] score={h.Score:F3}  kind={h.DocKind}  path={h.RelPath}\n" +
            $"breadcrumb: {h.Breadcrumb}\n\n" +
            h.Text));
    }

    [McpServerTool, Description(
        "Return relevant content for the top-ranked unique files matching the query. " +
        "Default mode groups the best chunks per file (no disk read). " +
        "When the question contains explicit full-content intent " +
        "(e.g. 'show me all details', 'full content of', 'whole file', 'explain everything about') " +
        "the server reads each top-ranked file in full from disk and returns the complete text. " +
        "Prefer this over QueryDocs when you need to reason over document context, not a single fragment.")]
    public async Task<string> ReadDocs(
        [Description("The search question or topic.")] string question,
        [Description("Optional bounded context filter — matched against doc_kind field.")] string? bc = null,
        [Description("Maximum unique files to return (default: 3, max: 5).")] int topFiles = 3,
        CancellationToken cancellationToken = default)
    {
        topFiles = Math.Clamp(topFiles, 1, 5);
        var fullMode = WantsFullContent(question);

        // Fetch generously so each file gets good per-file coverage in chunk mode.
        var queryVec = embedder.Embed(question);
        var hits = await store.SearchAsync(
            queryVec,
            topK: Math.Max(30, topFiles * 15),
            scoreThreshold: cfg.Query.ScoreThreshold,
            docKindFilter: bc,
            cancellationToken: cancellationToken);

        if (hits.Count == 0)
            return "No results found. Consider re-running the ingest script or broadening your query.";

        var ranked = hits
            .GroupBy(h => h.RelPath)
            .Select(g => new { RelPath = g.Key, BestScore = g.Max(h => h.Score), Chunks = g.OrderByDescending(h => h.Score).ToList() })
            .OrderByDescending(x => x.BestScore)
            .Take(topFiles)
            .ToList();

        var sections = new List<string>();
        foreach (var f in ranked)
        {
            var first = f.Chunks[0];
            if (fullMode)
            {
                var abs = Path.Combine(cfg.Workspace, f.RelPath);
                string body;
                try { body = await File.ReadAllTextAsync(abs, cancellationToken); }
                catch (Exception ex) { body = $"[ERROR: could not read file — {ex.Message}]"; }
                sections.Add(
                    $"# {f.RelPath}\n" +
                    $"score={f.BestScore:F3}  kind={first.DocKind}  mode=full  size={body.Length} chars\n\n" +
                    body);
            }
            else
            {
                var chunkTexts = f.Chunks.Take(8).Select((c, i) =>
                    $"## chunk {i + 1}  score={c.Score:F3}\n{c.Text}");
                sections.Add(
                    $"# {f.RelPath}\n" +
                    $"score={f.BestScore:F3}  kind={first.DocKind}  mode=chunks  chunks={Math.Min(f.Chunks.Count, 8)}\n\n" +
                    string.Join("\n\n", chunkTexts));
            }
        }

        return string.Join("\n\n===\n\n", sections);
    }

    private static readonly System.Text.RegularExpressions.Regex FullIntentRe =
        new(@"\b(all details|full details|full content|full text|entire|whole file|show me all|explain everything|everything about|complete picture|all about|deep dive|in full|from start to finish)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static bool WantsFullContent(string question) => FullIntentRe.IsMatch(question);

    [McpServerTool, Description(
        "Return all indexed chunks for a specific ADR, ordered by start line. " +
        "Equivalent to reading the full ADR with all its amendments in chronological order. " +
        "Pass the 4-digit ADR number (e.g. '0016').")]
    public async Task<string> GetAdrHistory(
        [Description("4-digit ADR ID (e.g. '0016' or '16').")] string adrId,
        CancellationToken cancellationToken = default)
    {
        // Normalise to 4-digit zero-padded.
        adrId = adrId.TrimStart('0').PadLeft(4, '0');

        var queryVec = embedder.Embed($"ADR {adrId}");
        var hits = await store.SearchAsync(
            queryVec,
            topK: 50,          // generous upper bound — amendments can be large
            scoreThreshold: 0, // no threshold filtering for history retrieval
            adrIdFilter: adrId,
            cancellationToken: cancellationToken);

        if (hits.Count == 0)
            return $"No chunks found for ADR {adrId}. Ensure the ADR is indexed.";

        return $"# ADR {adrId} — {hits[0].DocTitle}\n\n" +
               string.Join("\n\n---\n\n", hits.Select(h =>
                   $"**{h.Breadcrumb}** ({h.DocKind})\n\n{h.Text}"));
    }

    [McpServerTool, Description(
        "List all ADR IDs present in the index. " +
        "Use this to discover what ADRs exist before fetching a specific one.")]
    public async Task<string> ListAdrs(CancellationToken cancellationToken = default)
    {
        // Scroll all points with adr_id payload set, collect distinct IDs.
        // We do a broad search with a zero vector and high limit as a workaround
        // until Qdrant.Client exposes a scroll API in this version.
        var zeroVec = new float[embedder.Dimensions];
        var hits = await store.SearchAsync(
            zeroVec,
            topK: 200,
            scoreThreshold: 0,
            cancellationToken: cancellationToken);

        var adrs = hits
            .Where(h => h.AdrId is not null)
            .GroupBy(h => h.AdrId!)
            .OrderBy(g => g.Key)
            .Select(g => $"ADR-{g.Key}  ({g.First().DocTitle})");

        var list = adrs.ToList();
        return list.Count == 0
            ? "No ADRs found in the index."
            : $"Found {list.Count} ADR(s):\n\n" + string.Join("\n", list);
    }
}
