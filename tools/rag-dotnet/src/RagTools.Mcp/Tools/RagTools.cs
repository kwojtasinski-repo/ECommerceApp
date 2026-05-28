using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using RagTools.Core;
using RagTools.Core.Adrs;
using RagTools.Core.History;
using RagTools.Core.Query;
using RagTools.Core.ReadDocs;

namespace RagTools.Mcp.Tools;

/// <summary>
/// MCP tools exposed to Copilot. Pure delegation layer — each method clamps inputs,
/// builds the request DTO, calls one application service, and projects the typed
/// outcome to its JSON wire shape. All wire-shape concerns live in
/// <see cref="RagToolsProjector"/>; all orchestration lives in the four
/// <c>IRag*Service</c> implementations.
///
/// Every tool body is wrapped in <see cref="McpToolGuard.RunAsync{T}"/> so the
/// same sanitized error envelope is produced for any failure mode.
/// </summary>
[McpServerToolType]
public sealed class RagTools(
    IRagQueryService    queryService,
    IRagReadDocsService readDocsService,
    IRagHistoryService  historyService,
    IRagListService     listService,
    RagSession          session,
    ILogger<RagTools>   logger)
{
    // Hard cap for the free-form question string. Numeric caps for top_k /
    // top_files live next to their service (e.g. RagQueryService.MaxTopK).
    private const int MaxQuestionChars = 4096;

    [McpServerTool, Description(
        "Semantic search across project documentation (ADRs, architecture, patterns, reference, roadmap). " +
        "Returns the top-k most relevant chunks with breadcrumb, file path, line range, and text. " +
        "Use topic to substring-filter by bounded context or topic (matched against breadcrumb and doc title). " +
        "Follow up with ReadDocs to get full file content or grouped chunk view.")]
    public Task<string> QueryDocs(
        [Description("The search question or topic.")] string question,
        [Description("Optional substring filter matched against breadcrumb and doc title (e.g. 'Orders', 'Pricing').")] string? topic = null,
        [Description("Maximum number of results to return (default: 5, max: 20).")] int top_k = 5,
        CancellationToken cancellationToken = default)
    {
        top_k = Math.Clamp(top_k, 1, RagQueryService.MaxTopK);
        question = CapQuestion(question);
        logger.LogDebug("QueryDocs: collection={Collection} topic={Topic} topK={TopK}", session.Collection, topic, top_k);
        return McpToolGuard.RunAsync(logger, nameof(QueryDocs), async ct =>
        {
            var request = new QueryRequest(session.Collection, question, topic, top_k);
            var outcome = await queryService.QueryAsync(request, ct);
            return McpJson.Serialize(RagToolsProjector.ProjectQuery(outcome));
        }, cancellationToken);
    }

    [McpServerTool, Description(
        "L2 fast path: run a RAG query and return a formatted markdown payload plus a deterministic " +
        "source label, ready for context-mode caching via ctx_index(content=<markdown>, source=<source>). " +
        "The caller (Copilot) performs the ctx_index call. Subsequent recalls use " +
        "ctx_search(source=\"rag-cache-...\") and avoid re-billing RAG. " +
        "Source labels: rag-cache-adr<NNNN>-<hash8> when the question mentions an ADR id, " +
        "rag-cache-<slug(bc)>-<hash8> when bc is set, otherwise rag-cache-q-<hash8>.")]
    public Task<string> QueryDocsCached(
        [Description("The search question or topic.")] string question,
        [Description("Optional bounded-context / topic substring filter (matched against breadcrumb and doc title).")] string? bc = null,
        [Description("Maximum unique files to summarise (default: 3, max: 5).")] int top_files = 3,
        CancellationToken cancellationToken = default)
    {
        top_files = Math.Clamp(top_files, 1, RagReadDocsService.MaxTopFiles);
        question = CapQuestion(question);
        // Public IRagQueryService caps top_k at MaxTopK (45 as of B2 2026-05-28; matches Python's
        // max(30, top_files*15) for top_files=3). We clamp to the public service's ceiling so the
        // wrapper is parity-aligned with Python's query_docs_cached.
        var topK = Math.Clamp(top_files * 15, 1, RagQueryService.MaxTopK);
        var capturedTopFiles = top_files;
        var capturedBc = bc;
        var capturedQuestion = question;
        logger.LogDebug(
            "QueryDocsCached: collection={Collection} bc={Bc} topFiles={TopFiles} topK={TopK}",
            session.Collection, bc, top_files, topK);
        return McpToolGuard.RunAsync(logger, nameof(QueryDocsCached), async ct =>
        {
            var request = new QueryRequest(session.Collection, capturedQuestion, capturedBc, topK);
            var outcome = await queryService.QueryAsync(request, ct);
            return McpJson.Serialize(
                RagToolsProjector.ProjectQueryCached(outcome, capturedQuestion, capturedBc, capturedTopFiles, DateTime.UtcNow));
        }, cancellationToken);
    }

    [McpServerTool, Description(
        "Return relevant content for the top-ranked unique files matching the query. " +
        "Default mode groups the best chunks per file (no disk read). " +
        "When the question contains explicit full-content intent " +
        "(e.g. 'show me all details', 'full content of', 'whole file', 'explain everything about') " +
        "the server first fetches from Qdrant (stored at ingest time), then falls back to disk. " +
        "Prefer this over QueryDocs when you need to reason over document context, not a single fragment.")]
    public Task<string> ReadDocs(
        [Description("The search question or topic.")] string question,
        [Description("Optional topic / bounded-context substring filter \u2014 matched against breadcrumb and doc title.")] string? topic = null,
        [Description("Maximum unique files to return (default: 3, max: 5).")] int top_files = 3,
        CancellationToken cancellationToken = default)
    {
        top_files = Math.Clamp(top_files, 1, RagReadDocsService.MaxTopFiles);
        question = CapQuestion(question);
        logger.LogDebug("ReadDocs: collection={Collection} topic={Topic} topFiles={TopFiles}", session.Collection, topic, top_files);
        return McpToolGuard.RunAsync(logger, nameof(ReadDocs), async ct =>
        {
            var request = new ReadDocsRequest(session.Collection, question, topic, top_files);
            var outcome = await readDocsService.ReadAsync(request, ct);
            return McpJson.Serialize(RagToolsProjector.ProjectReadDocs(outcome));
        }, cancellationToken);
    }

    [McpServerTool, Description(
        "Return all indexed chunks for a document group identified by a history ID " +
        "(e.g. ADR number, RFC number). Chunks are returned in chronological order " +
        "(sorted by start_line). The grouping field is collection-defined (defaults to " +
        "'adr_id').")]
    public Task<string> GetHistory(
        [Description("History ID (e.g. '0016', 'RFC-003'). Matched against the collection's configured history field.")] string id,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("GetHistory: collection={Collection} id={Id}", session.Collection, id);
        return McpToolGuard.RunAsync(logger, nameof(GetHistory), async ct =>
        {
            var request = new HistoryRequest(session.Collection, id);
            var outcome = await historyService.GetAsync(request, ct);
            return McpJson.Serialize(RagToolsProjector.ProjectHistory(outcome));
        }, cancellationToken);
    }

    [McpServerTool, Description(
        "List all ADRs indexed in the collection with id, title, main file path, and amendment count. " +
        "Reads from the Qdrant index — results reflect what is currently ingested. " +
        "Use for orientation queries like 'what ADRs exist?' before calling GetHistory.")]
    public Task<string> ListAdrs(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("ListAdrs: collection={Collection}", session.Collection);
        return McpToolGuard.RunAsync(logger, nameof(ListAdrs), async ct =>
        {
            var request = new AdrListRequest(session.Collection);
            var outcome = await listService.ListAsync(request, ct);
            return McpJson.Serialize(RagToolsProjector.ProjectList(outcome));
        }, cancellationToken);
    }

    private static string CapQuestion(string? question)
        => question is { Length: > MaxQuestionChars } ? question[..MaxQuestionChars] : (question ?? string.Empty);
}
