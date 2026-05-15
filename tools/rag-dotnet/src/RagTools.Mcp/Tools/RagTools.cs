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
