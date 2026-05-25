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
        top_k = Math.Clamp(top_k, 1, RagQueryService.MaxTopK);
        logger.LogDebug("QueryDocs: collection={Collection} bc={Bc} topK={TopK}", session.Collection, bc, top_k);

        var request = new QueryRequest(session.Collection, question, bc, top_k);
        var outcome = await queryService.QueryAsync(request, cancellationToken);
        return McpJson.Serialize(RagToolsProjector.ProjectQuery(outcome));
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
        top_files = Math.Clamp(top_files, 1, RagReadDocsService.MaxTopFiles);
        logger.LogDebug("ReadDocs: collection={Collection} bc={Bc} topFiles={TopFiles}", session.Collection, bc, top_files);

        var request = new ReadDocsRequest(session.Collection, question, bc, top_files);
        var outcome = await readDocsService.ReadAsync(request, cancellationToken);
        return McpJson.Serialize(RagToolsProjector.ProjectReadDocs(outcome));
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
        logger.LogDebug("GetHistory: collection={Collection} id={Id}", session.Collection, id);

        var request = new HistoryRequest(session.Collection, id);
        var outcome = await historyService.GetAsync(request, cancellationToken);
        return McpJson.Serialize(RagToolsProjector.ProjectHistory(outcome));
    }

    [McpServerTool, Description(
        "List all ADRs indexed in the collection with id, title, main file path, and amendment count. " +
        "Reads from the Qdrant index — results reflect what is currently ingested. " +
        "Use for orientation queries like 'what ADRs exist?' before calling GetHistory.")]
    public async Task<string> ListAdrs(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("ListAdrs: collection={Collection}", session.Collection);

        var request = new AdrListRequest(session.Collection);
        var outcome = await listService.ListAsync(request, cancellationToken);
        return McpJson.Serialize(RagToolsProjector.ProjectList(outcome));
    }
}
