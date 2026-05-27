using RagTools.Core.Adrs;
using RagTools.Core.History;
using RagTools.Core.Query;
using RagTools.Core.ReadDocs;

namespace RagTools.Mcp.Tools;

/// <summary>
/// Wire-shape projector for MCP tools. Owns the snake_case anonymous-type layout
/// that the four MCP tools return to Copilot. All four tools delegate to one of
/// these methods so the JSON contract lives in one auditable place.
///
/// Failures share a single envelope: <c>{ error, code, details? }</c>. <c>details</c>
/// is omitted when null or empty so the wire format stays compact.
/// </summary>
internal static class RagToolsProjector
{
    // ── query_docs ───────────────────────────────────────────────────────────

    public static object ProjectQuery(QueryOutcome outcome) => outcome switch
    {
        QueryOutcome.Success s when s.Response.Hits.Count == 0 =>
            new
            {
                hits = Array.Empty<object>(),
                message = "No results found. Consider re-running the ingest script or broadening your query.",
            },

        QueryOutcome.Success s => new
        {
            hits = s.Response.Hits.Select(h => new
            {
                rank       = h.Rank,
                score      = h.Score,
                doc_kind   = h.DocKind,
                rel_path   = h.RelPath,
                breadcrumb = h.Breadcrumb,
                start_line = h.StartLine,
                end_line   = h.EndLine,
                text       = h.Text,
            }).ToArray(),
        },

        QueryOutcome.Failure f => Failure(f.Message, f.Error.ToString(), f.Details),

        _ => throw new InvalidOperationException($"Unhandled QueryOutcome: {outcome.GetType().Name}"),
    };

    // ── read_docs ────────────────────────────────────────────────────────────

    public static object ProjectReadDocs(ReadDocsOutcome outcome) => outcome switch
    {
        ReadDocsOutcome.Success s when s.Response.Files.Count == 0 =>
            new
            {
                files = Array.Empty<object>(),
                message = "No results found. Consider re-running the ingest script or broadening your query.",
            },

        ReadDocsOutcome.Success s => new
        {
            files = s.Response.Files.Select(f => f.Mode == ReadDocsMode.Full
                ? (object)new
                {
                    rel_path = f.RelPath,
                    score    = f.Score,
                    doc_kind = f.DocKind,
                    mode     = "full",
                    content  = f.Content,
                }
                : new
                {
                    rel_path = f.RelPath,
                    score    = f.Score,
                    doc_kind = f.DocKind,
                    mode     = "chunks",
                    chunks   = f.Chunks.Select(c => new
                    {
                        rank       = c.Rank,
                        score      = c.Score,
                        start_line = c.StartLine,
                        text       = c.Text,
                    }).ToArray(),
                }).ToArray(),
        },

        ReadDocsOutcome.Failure f => Failure(f.Message, f.Error.ToString(), f.Details),

        _ => throw new InvalidOperationException($"Unhandled ReadDocsOutcome: {outcome.GetType().Name}"),
    };

    // ── get_history ──────────────────────────────────────────────────────────

    public static object ProjectHistory(HistoryOutcome outcome) => outcome switch
    {
        HistoryOutcome.Success s when s.Response.Chunks.Count == 0 => new
        {
            id            = s.Response.Id,
            history_field = s.Response.HistoryField,
            chunk_count   = 0,
            chunks        = Array.Empty<object>(),
            message       = $"No chunks found for {s.Response.HistoryField}={s.Response.Id}. Ensure the document is indexed.",
        },

        HistoryOutcome.Success s => new
        {
            id            = s.Response.Id,
            history_field = s.Response.HistoryField,
            chunk_count   = s.Response.Chunks.Count,
            chunks        = s.Response.Chunks.Select(c => new
            {
                rel_path   = c.RelPath,
                breadcrumb = c.Breadcrumb,
                doc_kind   = c.DocKind,
                start_line = c.StartLine,
                text       = c.Text,
            }).ToArray(),
        },

        HistoryOutcome.Failure f => Failure(f.Message, f.Error.ToString(), f.Details),

        _ => throw new InvalidOperationException($"Unhandled HistoryOutcome: {outcome.GetType().Name}"),
    };

    // ── list_adrs ────────────────────────────────────────────────────────────

    public static object ProjectList(AdrListOutcome outcome) => outcome switch
    {
        AdrListOutcome.Success s => new
        {
            adrs = s.Response.Adrs.Select(a => new
            {
                id         = a.Id,
                title      = a.Title,
                main_file  = a.MainFile,
                amendments = a.Amendments,
                examples   = a.Examples,
            }).ToArray(),
            count = s.Response.Adrs.Count,
        },

        AdrListOutcome.Failure f => Failure(f.Message, f.Error.ToString(), f.Details),

        _ => throw new InvalidOperationException($"Unhandled AdrListOutcome: {outcome.GetType().Name}"),
    };

    // ── query_docs_cached ────────────────────────────────────────────────────

    public static object ProjectQueryCached(QueryOutcome outcome, string question, string? bc, int topFiles, DateTime utcNow) => outcome switch
    {
        QueryOutcome.Success s when s.Response.Hits.Count == 0 =>
            new
            {
                files_count = 0,
                chunks_count = 0,
                query = question,
                bc,
                markdown = string.Empty,
                message = "No results found. Consider re-running the ingest script or broadening your query.",
            },

        QueryOutcome.Success s =>
            (object)QueryDocsCachedFormatter.Build(question, bc, topFiles, s.Response.Hits, utcNow).ToProjection(),

        QueryOutcome.Failure f => Failure(f.Message, f.Error.ToString(), f.Details),

        _ => throw new InvalidOperationException($"Unhandled QueryOutcome: {outcome.GetType().Name}"),
    };

    // ── shared failure envelope ──────────────────────────────────────────────

    private static Dictionary<string, object?> Failure(
        string message, string code, IReadOnlyDictionary<string, object?>? details)
    {
        var body = new Dictionary<string, object?>
        {
            ["error"] = message,
            ["code"]  = code,
        };
        if (details is { Count: > 0 })
            body["details"] = details;
        return body;
    }
}
