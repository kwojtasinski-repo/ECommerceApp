using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using RagTools.Core;

namespace RagTools.Mcp.Tools;

/// <summary>
/// MCP tools exposed to Copilot. Mirrors the Python mcp_server.py tools:
///   query_docs      â€” semantic search across all indexed documentation
///   read_docs       â€” grouped-by-file search (Qdrant content or disk fallback)
///   get_adr_history â€” fetch all chunks for a specific ADR ID, ordered by start line
///   list_adrs       â€” list all ADRs from disk (accurate, not index-dependent)
///
/// Uses <see cref="IDocumentStore"/> + <see cref="RagSession"/> for multi-tenant support:
///   - Collection is resolved per-session from ?project= query parameter (Step 9)
///   - ReadDocs uses IDocumentStore.FetchContentAsync then falls back to disk (Step 10)
/// </summary>
[McpServerToolType]
public sealed class RagTools(
    OnnxEmbedder embedder,
    IDocumentStore store,
    RagSession session,
    RagConfig cfg,
    ILogger<RagTools> logger)
{
    // Loaded once per server lifetime â€” returns Empty if GlossaryPath is null or file absent.
    private readonly MultilingualGlossary _glossary = MultilingualGlossary.Load(cfg.GlossaryPath);

    [McpServerTool, Description(
        "Semantic search across project documentation (ADRs, architecture, patterns, reference, roadmap). " +
        "Returns the top-k most relevant chunks with breadcrumb, file path, line range, and text. " +
        "Use bc to substring-filter by bounded context or topic (matched against breadcrumb and doc title). " +
        "Follow up with ReadDocs to get full file content or grouped chunk view.")]
    public async Task<string> QueryDocs(
        [Description("The search question or topic.")] string question,
        [Description("Optional substring filter matched against breadcrumb and doc title (e.g. 'Orders', 'Pricing').")] string? bc = null,
        [Description("Maximum number of results to return (default: 5, max: 20).")] int topK = 5,
        CancellationToken cancellationToken = default)
    {
        topK = Math.Clamp(topK, 1, 20);
        var collection = session.Collection;

        // Widen fetch pool when bc filter is active to compensate for post-filter loss.
        var fetchK = bc is not null ? Math.Max(cfg.Query.FetchK, topK * 3) : cfg.Query.FetchK;
        logger.LogDebug("QueryDocs: collection={Collection} question={Question} bc={Bc} topK={TopK} fetchK={FetchK}",
            collection, question, bc, topK, fetchK);

        var queryVec = embedder.Embed(_glossary.Expand(question));
        var opts = new SearchOptions(fetchK, cfg.Query.ScoreThreshold);
        var allHits = await store.SearchAsync(collection, queryVec, opts, cancellationToken);

        var weighted = ApplyWeights(allHits);
        var hits = bc is not null
            ? weighted.Where(h => MatchesBc(h, bc)).Take(topK).ToList()
            : weighted.ToList();

        logger.LogInformation("QueryDocs: returned {Count}/{Total} hits for '{Question}'", hits.Count, allHits.Count, question);

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
        "the server first fetches from Qdrant (stored at ingest time), then falls back to disk. " +
        "Prefer this over QueryDocs when you need to reason over document context, not a single fragment.")]
    public async Task<string> ReadDocs(
        [Description("The search question or topic.")] string question,
        [Description("Optional bounded context filter â€” matched against doc_kind field.")] string? bc = null,
        [Description("Maximum unique files to return (default: 3, max: 5).")] int topFiles = 3,
        CancellationToken cancellationToken = default)
    {
        topFiles = Math.Clamp(topFiles, 1, 5);
        var fullMode = WantsFullContent(question);
        var collection = session.Collection;
        logger.LogDebug("ReadDocs: collection={Collection} question={Question} bc={Bc} topFiles={TopFiles} fullMode={FullMode}",
            collection, question, bc, topFiles, fullMode);

        var queryVec = embedder.Embed(_glossary.Expand(question));
        var opts = new SearchOptions(Math.Max(30, topFiles * 15), cfg.Query.ScoreThreshold);
        var rawHits = await store.SearchAsync(collection, queryVec, opts, cancellationToken);

        var weighted = ApplyWeights(rawHits);
        var hits = bc is not null
            ? weighted.Where(h => MatchesBc(h, bc)).ToList()
            : weighted.ToList();

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
                // Step 10: try Qdrant content point first, fall back to disk.
                string body;
                var contentDoc = await store.FetchContentAsync(collection, f.RelPath, cancellationToken);
                if (contentDoc is not null)
                {
                    body = contentDoc.Content;
                    logger.LogDebug("ReadDocs: Qdrant content hit {RelPath} ({Size} chars)", f.RelPath, body.Length);
                }
                else
                {
                    var abs = Path.Combine(cfg.Workspace, f.RelPath);
                    try
                    {
                        body = await File.ReadAllTextAsync(abs, cancellationToken);
                        logger.LogDebug("ReadDocs: disk fallback {RelPath} ({Size} chars)", f.RelPath, body.Length);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "ReadDocs: could not read file {RelPath}", f.RelPath);
                        body = $"[ERROR: could not read file â€” {ex.Message}]";
                    }
                }
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

        logger.LogInformation("ReadDocs: returned {Count} file(s) for '{Question}'", sections.Count, question);
        return string.Join("\n\n===\n\n", sections);
    }

    /// <summary>
    /// Multiplies each hit's score by the configured weight for its path
    /// (from config.yaml ranking.weights â€” first matching glob wins, default 1.0).
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
        "Return all indexed chunks for a specific ADR, ordered by start line. " +
        "Equivalent to reading the full ADR with all its amendments in chronological order. " +
        "Pass the 4-digit ADR number (e.g. '0016').")]
    public async Task<string> GetAdrHistory(
        [Description("4-digit ADR ID (e.g. '0016' or '16').")] string adrId,
        CancellationToken cancellationToken = default)
    {
        // Normalise to 4-digit zero-padded.
        adrId = adrId.TrimStart('0').PadLeft(4, '0');
        var collection = session.Collection;
        logger.LogDebug("GetAdrHistory: collection={Collection} adrId={AdrId}", collection, adrId);

        var queryVec = embedder.Embed($"ADR {adrId}");
        var opts = new SearchOptions(TopK: 50, ScoreThreshold: 0, AdrIdFilter: adrId);
        var hits = await store.SearchAsync(collection, queryVec, opts, cancellationToken);

        if (hits.Count == 0)
        {
            logger.LogWarning("GetAdrHistory: no chunks for ADR {AdrId} in {Collection}", adrId, collection);
            return $"No chunks found for ADR {adrId}. Ensure the ADR is indexed.";
        }

        var ordered = hits.OrderBy(h => h.StartLine).ToList();
        logger.LogInformation("GetAdrHistory: ADR {AdrId} returned {Count} chunks", adrId, ordered.Count);
        return $"# ADR {adrId} â€” {ordered[0].DocTitle}\n\n" +
               string.Join("\n\n---\n\n", ordered.Select(h =>
                   $"**{h.Breadcrumb}** ({h.DocKind})\n\n{h.Text}"));
    }

    [McpServerTool, Description(
        "List all ADRs in the repository with id, title, and amendment count. " +
        "Reads the docs/adr/ folder from disk â€” always accurate, not limited by index coverage. " +
        "Use for orientation queries like 'what ADRs exist?' before calling GetAdrHistory.")]
    public Task<string> ListAdrs(CancellationToken cancellationToken = default)
    {
        var adrFolder = Path.Combine(cfg.Workspace, "docs", "adr");
        logger.LogDebug("ListAdrs: scanning {AdrFolder}", adrFolder);
        if (!Directory.Exists(adrFolder))
            return Task.FromResult($"ADR folder not found at: {adrFolder}. Check RAG_WORKSPACE.");

        var rows = new List<string>();
        foreach (var folder in Directory.EnumerateDirectories(adrFolder).OrderBy(d => d))
        {
            var dirName = Path.GetFileName(folder);
            if (!System.Text.RegularExpressions.Regex.IsMatch(dirName, @"^\d{4}$")) continue;

            var mainFiles = Directory.GetFiles(folder, $"{dirName}-*.md").OrderBy(f => f).ToList();
            var title = string.Empty;
            if (mainFiles.Count > 0)
            {
                var text = File.ReadAllText(mainFiles[0]);
                var m = TitleRe.Match(text);
                if (m.Success) title = m.Groups[1].Value.Trim();
            }
            var amendmentsDir = Path.Combine(folder, "amendments");
            var amendCount = Directory.Exists(amendmentsDir)
                ? Directory.GetFiles(amendmentsDir, "*.md").Length : 0;
            var amendSuffix = amendCount > 0 ? $"  [{amendCount} amendment(s)]" : string.Empty;
            rows.Add($"ADR-{dirName}  {title}{amendSuffix}");
        }

        return Task.FromResult(
            rows.Count == 0
                ? "No ADRs found."
                : $"Found {rows.Count} ADR(s):\n\n" + string.Join("\n", rows));
    }

    private static readonly System.Text.RegularExpressions.Regex TitleRe =
        new(@"^#\s+(?:ADR-\d+\s*[â€”:-]\s*)?(.+?)\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.Compiled);
}
