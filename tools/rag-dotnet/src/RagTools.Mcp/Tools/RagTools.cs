using System.ComponentModel;
using ModelContextProtocol.Server;
using RagTools.Core;

namespace RagTools.Mcp.Tools;

/// <summary>
/// MCP tools exposed to Copilot. Mirrors the Python mcp_server.py tools:
///   query_docs      — semantic search across all indexed documentation
///   read_docs       — grouped-by-file search (chunk or full-file mode)
///   get_adr_history — fetch all chunks for a specific ADR ID, ordered by start line
///   list_adrs       — list all ADRs from disk (accurate, not index-dependent)
/// </summary>
[McpServerToolType]
public sealed class RagTools(OnnxEmbedder embedder, QdrantStore store, RagConfig cfg)
{
    // Loaded once per server lifetime — returns Empty if GlossaryPath is null or file absent.
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

        // Fetch more when bc filter is active to compensate for post-filtering loss.
        var fetchK = bc is not null ? topK * 3 : topK;
        var queryVec = embedder.Embed(_glossary.Expand(question));
        var allHits = await store.SearchAsync(
            queryVec,
            fetchK,
            cfg.Query.ScoreThreshold,
            cancellationToken: cancellationToken);

        var weighted = ApplyWeights(allHits);
        var hits = bc is not null
            ? weighted.Where(h => MatchesBc(h, bc)).Take(topK).ToList()
            : weighted.ToList();

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
        // No Qdrant-level filter: bc is a substring post-filter on breadcrumb/DocTitle.
        var queryVec = embedder.Embed(_glossary.Expand(question));
        var rawHits = await store.SearchAsync(
            queryVec,
            topK: Math.Max(30, topFiles * 15),
            scoreThreshold: cfg.Query.ScoreThreshold,
            cancellationToken: cancellationToken);

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

    /// <summary>
    /// Multiplies each hit's score by the configured weight for its path
    /// (from config.yaml ranking.weights — first matching glob wins, default 1.0).
    /// Re-sorts descending so the caller gets a ready-ranked list.
    /// </summary>
    private IReadOnlyList<SearchHit> ApplyWeights(IReadOnlyList<SearchHit> hits) =>
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
    private static bool MatchesBc(SearchHit h, string bc)
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

        var queryVec = embedder.Embed($"ADR {adrId}");
        var hits = await store.SearchAsync(
            queryVec,
            topK: 50,          // generous upper bound — amendments can be large
            scoreThreshold: 0, // no threshold filtering for history retrieval
            adrIdFilter: adrId,
            cancellationToken: cancellationToken);

        if (hits.Count == 0)
            return $"No chunks found for ADR {adrId}. Ensure the ADR is indexed.";

        // Order by start line so the output reads top-to-bottom like the source document.
        var ordered = hits.OrderBy(h => h.StartLine).ToList();
        return $"# ADR {adrId} — {ordered[0].DocTitle}\n\n" +
               string.Join("\n\n---\n\n", ordered.Select(h =>
                   $"**{h.Breadcrumb}** ({h.DocKind})\n\n{h.Text}"));
    }

    [McpServerTool, Description(
        "List all ADRs in the repository with id, title, and amendment count. " +
        "Reads the docs/adr/ folder from disk — always accurate, not limited by index coverage. " +
        "Use for orientation queries like 'what ADRs exist?' before calling GetAdrHistory.")]
    public Task<string> ListAdrs(CancellationToken cancellationToken = default)
    {
        // Read from disk — accurate and complete regardless of index state.
        // Mirrors Python _tool_list_adrs which iterates docs/adr/ directly.
        var adrFolder = Path.Combine(cfg.Workspace, "docs", "adr");
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
        new(@"^#\s+(?:ADR-\d+\s*[—:-]\s*)?(.+?)\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.Compiled);
}
