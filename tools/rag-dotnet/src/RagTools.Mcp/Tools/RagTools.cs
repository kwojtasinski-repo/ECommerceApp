using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using RagTools.Core;
using RagTools.Core.ContentSources;

namespace RagTools.Mcp.Tools;

/// <summary>
/// MCP tools exposed to Copilot. Mirrors the Python mcp_server.py tools:
///   query_docs      — semantic search across all indexed documentation
///   read_docs       — grouped-by-file search (content via IContentSource)
///   get_adr_history — fetch all chunks for a specific ADR ID, ordered by start line
///   list_adrs       — list all ADRs from Qdrant index via IDocumentStore.ListAdrsAsync
///
/// Uses <see cref="IDocumentStore"/> + <see cref="RagSession"/> for multi-tenant support:
///   - Collection is resolved per-request via ICollectionResolver (HttpCollectionResolver in HTTP mode)
///   - ReadDocs full-content is read via <see cref="IContentSource"/> (QdrantContentSource or DiskContentSource)
/// </summary>
[McpServerToolType]
public sealed class RagTools(
    IEmbedder embedder,
    IDocumentStore store,
    RagSession session,
    RagConfig cfg,
    IEnumerable<IResultPostprocessor> resultPostprocessors,
    IContentSource contentSource,
    ILogger<RagTools> logger)
{

    [McpServerTool, Description(
        "Semantic search across project documentation (ADRs, architecture, patterns, reference, roadmap). " +
        "Returns the top-k most relevant chunks with breadcrumb, file path, line range, and text. " +
        "Use bc to substring-filter by bounded context or topic (matched against breadcrumb and doc title). " +
        "Follow up with ReadDocs to get full file content or grouped chunk view.")]
    public async Task<string> QueryDocs(
        [Description("The search question or topic.")] string question,
        [Description("Optional substring filter matched against breadcrumb and doc title (e.g. 'Orders', 'Pricing').")] string? bc = null,
        [Description("Maximum number of results to return (default: 5, max: 20).")] int top_k = 5,
        CancellationToken cancellationToken = default)
    {
        top_k = Math.Clamp(top_k, 1, 20);
        var collection = session.Collection;

        // Widen fetch pool when bc filter is active to compensate for post-filter loss.
        var fetchK = bc is not null ? Math.Max(cfg.Query.FetchK, top_k * 3) : cfg.Query.FetchK;
        logger.LogDebug("QueryDocs: collection={Collection} question={Question} bc={Bc} topK={TopK} fetchK={FetchK}",
            collection, question, bc, top_k, fetchK);

        // Preprocessing (glossary expansion, truncation) runs inside the pipeline.
        var queryVec = await embedder.EmbedAsync(question, cancellationToken);
        var opts = new SearchOptions(fetchK, cfg.Query.ScoreThreshold);
        var allHits = await store.SearchAsync(collection, queryVec, opts, cancellationToken);

        var weighted = ApplyWeights(allHits);
        IReadOnlyList<DocumentSearchResult> hits = bc is not null
            ? weighted.Where(h => MatchesBc(h, bc)).Take(top_k).ToList()
            : weighted.ToList();

        // D3: run post-retrieval processors (re-rank, filter, augment).
        var ctx = new QueryContext(collection, question, bc, top_k, fetchK);
        foreach (var pp in resultPostprocessors)
            hits = await pp.ProcessAsync(hits, ctx, cancellationToken);

        logger.LogInformation("QueryDocs: returned {Count}/{Total} hits for '{Question}'", hits.Count, allHits.Count, question);

        if (hits.Count == 0)
            return JsonSerializer.Serialize(new { hits = Array.Empty<object>(), message = "No results found. Consider re-running the ingest script or broadening your query." });

        var result = new
        {
            hits = hits.Select((h, i) => new
            {
                rank = i + 1,
                score = Math.Round(h.Score, 3),
                doc_kind = h.DocKind,
                rel_path = h.RelPath,
                breadcrumb = h.Breadcrumb,
                text = h.Text
            }).ToArray()
        };
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description(
        "Return relevant content for the top-ranked unique files matching the query. " +
        "Default mode groups the best chunks per file (no disk read). " +
        "When the question contains explicit full-content intent " +
        "(e.g. 'show me all details', 'full content of', 'whole file', 'explain everything about') " +
        "the server first fetches from Qdrant (stored at ingest time), then falls back to disk. " +
        "Prefer this over QueryDocs when you need to reason over document context, not a single fragment.")]
    public async Task<string> ReadDocs(
        [Description("The search question or topic.")] string question,
        [Description("Optional bounded context filter — matched against doc_kind field.")] string? bc = null,
        [Description("Maximum unique files to return (default: 3, max: 5).")] int top_files = 3,
        CancellationToken cancellationToken = default)
    {
        top_files = Math.Clamp(top_files, 1, 5);
        var fullMode = WantsFullContent(question);
        var collection = session.Collection;
        logger.LogDebug("ReadDocs: collection={Collection} question={Question} bc={Bc} topFiles={TopFiles} fullMode={FullMode}",
            collection, question, bc, top_files, fullMode);

        var queryVec = await embedder.EmbedAsync(question, cancellationToken);
        var opts = new SearchOptions(Math.Max(30, top_files * 15), cfg.Query.ScoreThreshold);
        var rawHits = await store.SearchAsync(collection, queryVec, opts, cancellationToken);

        var weighted = ApplyWeights(rawHits);
        var hits = bc is not null
            ? weighted.Where(h => MatchesBc(h, bc)).ToList()
            : weighted.ToList();

        if (hits.Count == 0)
            return JsonSerializer.Serialize(new { hits = Array.Empty<object>(), message = "No results found. Consider re-running the ingest script or broadening your query." });

        var ranked = hits
            .GroupBy(h => h.RelPath)
            .Select(g => new { RelPath = g.Key, BestScore = g.Max(h => h.Score), Chunks = g.OrderByDescending(h => h.Score).ToList() })
            .OrderByDescending(x => x.BestScore)
            .Take(top_files)
            .ToList();

        var files = new List<object>();
        foreach (var f in ranked)
        {
            var first = f.Chunks[0];
            if (fullMode)
            {
                // IContentSource dispatches to QdrantContentSource (HTTP) or DiskContentSource (STDIO).
                var body = await contentSource.ReadAsync(collection, f.RelPath, cancellationToken)
                           ?? $"[Content unavailable for {f.RelPath}]";
                logger.LogDebug("ReadDocs: content {RelPath} ({Size} chars)", f.RelPath, body.Length);
                files.Add(new { rel_path = f.RelPath, score = Math.Round(f.BestScore, 3), doc_kind = first.DocKind, mode = "full", content = body });
            }
            else
            {
                var chunks = f.Chunks.Take(8).Select((c, i) => new { rank = i + 1, score = Math.Round(c.Score, 3), text = c.Text }).ToArray();
                files.Add(new { rel_path = f.RelPath, score = Math.Round(f.BestScore, 3), doc_kind = first.DocKind, mode = "chunks", chunks });
            }
        }

        logger.LogInformation("ReadDocs: returned {Count} file(s) for '{Question}'", files.Count, question);
        return JsonSerializer.Serialize(new { files });
    }

    /// <summary>
    /// Multiplies each hit's score by the configured weight for its path
    /// (from rag-config.yaml ranking.weights — first matching glob wins, default 1.0).
    /// Re-sorts descending so the caller gets a ready-ranked list.
    /// </summary>
    private IReadOnlyList<DocumentSearchResult> ApplyWeights(IReadOnlyList<DocumentSearchResult> hits) =>
        hits.Select(h => h with { Score = h.Score * cfg.GetWeight(h.RelPath) })
            .OrderByDescending(h => h.Score)
            .ToList();

    private static readonly System.Text.RegularExpressions.Regex FullIntentRe =
        new(@"\b(all details|full details|full content|full text|entire|whole file|show me all|explain everything|everything about|complete picture|all about|deep dive|in full|from start to finish)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static bool WantsFullContent(string question) => FullIntentRe.IsMatch(question);

    /// <summary>
    /// Post-filter: returns true when the hit's breadcrumb or doc title contains bc as a
    /// case-insensitive substring. Mirrors Python QueryEngine._matches_bc().
    /// </summary>
    private static bool MatchesBc(DocumentSearchResult h, string bc)
    {
        var lower = bc.ToLowerInvariant();
        return (h.Breadcrumb?.ToLowerInvariant().Contains(lower) ?? false)
            || (h.DocTitle?.ToLowerInvariant().Contains(lower) ?? false);
    }

    [McpServerTool, Description(
        "Return all indexed chunks for a document group identified by a history ID " +
        "(e.g. ADR number, RFC number). Chunks are returned in chronological order " +
        "(sorted by start_line). The grouping field is collection-defined (defaults to " +
        "'adr_id').")]
    public async Task<string> GetHistory(
        [Description("History ID (e.g. '0016', 'RFC-003'). Matched against the collection's configured history field.")] string id,
        CancellationToken cancellationToken = default)
    {
        var collection = session.Collection;
        logger.LogDebug("GetHistory: collection={Collection} id={Id}", collection, id);

        // Read history_field from the collection __config__ point (id=0).
        // Falls back to "adr_id" for collections ingested before P2-3.
        var historyField = "adr_id";
        try
        {
            var configPayload = await store.FetchConfigAsync(collection, cancellationToken);
            if (!string.IsNullOrEmpty(configPayload?.HistoryField))
            {
                historyField = configPayload.HistoryField;
            }
        }
        catch
        {
            // config point absent — use default
        }

        var queryVec = await embedder.EmbedAsync($"history {id}", cancellationToken);
        var opts = new SearchOptions(TopK: 50, ScoreThreshold: 0,
            HistoryFieldFilter: (historyField, id));
        var hits = await store.SearchAsync(collection, queryVec, opts, cancellationToken);

        if (hits.Count == 0)
        {
            logger.LogWarning("GetHistory: no chunks for {Field}={Id} in {Collection}", historyField, id, collection);
            return JsonSerializer.Serialize(new
            {
                id,
                history_field = historyField,
                chunk_count = 0,
                chunks = Array.Empty<object>(),
                message = $"No chunks found for {historyField}={id}. Ensure the document is indexed.",
            });
        }

        var ordered = hits.OrderBy(h => h.StartLine).ToList();
        logger.LogInformation("GetHistory: {Field}={Id} returned {Count} chunks", historyField, id, ordered.Count);
        var result = new
        {
            id,
            history_field = historyField,
            chunk_count = ordered.Count,
            chunks = ordered.Select(h => new
            {
                rel_path = h.RelPath,
                breadcrumb = h.Breadcrumb,
                doc_kind = h.DocKind,
                start_line = h.StartLine,
                text = h.Text,
            }).ToArray(),
        };
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description(
        "List all ADRs indexed in the collection with id, title, main file path, and amendment count. " +
        "Reads from the Qdrant index — results reflect what is currently ingested. " +
        "Use for orientation queries like 'what ADRs exist?' before calling GetHistory.")]
    public async Task<string> ListAdrs(CancellationToken cancellationToken = default)
    {
        var collection = session.Collection;
        logger.LogDebug("ListAdrs: collection={Collection}", collection);

        var summaries = await store.ListAdrsAsync(collection, cancellationToken);

        logger.LogInformation("ListAdrs: returned {Count} ADRs from {Collection}", summaries.Count, collection);
        var adrs = summaries.Select(a => new
        {
            adr_id         = a.Id,
            title          = a.Title,
            main_file      = a.MainFile,
            amendment_count = a.Amendments,
            example_count  = a.Examples,
        }).ToArray();

        return JsonSerializer.Serialize(new { adrs });
    }
}
