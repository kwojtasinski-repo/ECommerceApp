using System.ComponentModel;
using System.Text.Json;
using KgMcp.Core;
using ModelContextProtocol.Server;

namespace KgMcp.Server;

/// <summary>
/// Delegation shell over <see cref="KgGraphService"/>. No Cypher and no traversal logic lives
/// here on purpose — this layer only names the tool, describes it for the calling model, and
/// serializes the result.
/// </summary>
[McpServerToolType]
public sealed class KgTools(KgGraphService graph)
{
    [McpServerTool(Name = "GetNodeNeighbors"), Description("Returns every incoming and outgoing edge of one node, as a structural connection map. Start here when you know a type or endpoint name and need to see what it touches. An id matching several nodes returns an error naming the labels rather than guessing one.")]
    public Task<string> GetNodeNeighbors(
        [Description("Exact node id, e.g. a fully-qualified type name.")] string nodeId)
        => RunAsync("GetNodeNeighbors", () => graph.GetNodeNeighborsAsync(nodeId));

    [McpServerTool(Name = "GetBlastRadius"), Description("Returns what is structurally downstream of a node, i.e. what could be affected if it changes, with the hop distance for each result. maxDepth is clamped to 1..5 server-side. This is graph reachability only: it is not an effort estimate, a risk score, or a claim about test coverage.")]
    public Task<string> GetBlastRadius(
        [Description("Exact node id to start from.")] string nodeId,
        [Description("Hop limit, clamped to 1..5. Start at 2 and widen only if the answer is too narrow.")] int maxDepth = 3)
        => RunAsync("GetBlastRadius", () => graph.GetBlastRadiusAsync(nodeId, maxDepth));

    [McpServerTool(Name = "GetNodeDependencies"), Description("Returns what a node structurally depends on, by traversing edges in reverse, with hop distance. Use it before moving or deleting something. Depth is clamped to 1..5. Graph dependencies only — not runtime call graphs.")]
    public Task<string> GetNodeDependencies(
        [Description("Exact node id.")] string nodeId,
        [Description("Hop limit, clamped to 1..5.")] int maxDepth = 3)
        => RunAsync("GetNodeDependencies", () => graph.GetNodeDependenciesAsync(nodeId, maxDepth));

    [McpServerTool(Name = "GetModuleDependencies"), Description("Returns which other modules a module integrates with, and the specific Message or Query contract carrying each dependency. The ontology has no direct module-to-module edge, so these are derived through the contracts that actually cross the boundary; an empty result means no contract crosses, not that the modules are unrelated.")]
    public Task<string> GetModuleDependencies(
        [Description("Exact Module node id.")] string moduleId)
        => RunAsync("GetModuleDependencies", () => graph.GetModuleDependenciesAsync(moduleId));

    [McpServerTool(Name = "GetModuleOwnership"), Description("Returns everything a module contains — entities, repositories, actions, handlers — to establish which bounded context owns a concept before you add code to it.")]
    public Task<string> GetModuleOwnership(
        [Description("Exact Module node id.")] string moduleId)
        => RunAsync("GetModuleOwnership", () => graph.GetModuleOwnershipAsync(moduleId));

    [McpServerTool(Name = "GetActionExposure"), Description("Returns how a node is reached from outside: for an Action, the endpoints and pages exposing it; for a Job, the actions or handlers scheduling it. The traversal branches on the resolved label, so passing a node that is neither returns an explicit error instead of a misleading empty list.")]
    public Task<string> GetActionExposure(
        [Description("Exact Action or Job node id.")] string nodeId)
        => RunAsync("GetActionExposure", () => graph.GetActionExposureAsync(nodeId));

    [McpServerTool(Name = "GetOrphanContracts"), Description("Returns Message, Query, and Job contracts that are missing an edge the ontology leads you to expect, each with a confidence and a reason. Read the confidence before acting: 'high' is a genuine dead-contract signal, 'contradiction' is a declared trigger with no scheduler, and 'ambiguous' marks known false-positive classes — jobs triggered from the runtime ScheduledJob table, and contracts whose only caller is invisible to this ontology. Never delete code on an 'ambiguous' row alone.")]
    public Task<string> GetOrphanContracts()
        => RunAsync("GetOrphanContracts", () => graph.GetOrphanContractsAsync());

    [McpServerTool(Name = "GetJobSchedulers"), Description("Returns the static schedulers and declared trigger mode of a Job. An empty scheduler list does not mean the job never runs: Scheduled and Manual triggers live in the runtime ScheduledJob table, outside this static graph.")]
    public Task<string> GetJobSchedulers(
        [Description("Exact Job node id.")] string jobId)
        => RunAsync("GetJobSchedulers", () => graph.GetJobSchedulersAsync(jobId));

    [McpServerTool(Name = "GetGovernedActions"), Description("Returns what a Role or Policy governs: each governed endpoint or page, and the action behind it. Authorization attaches to the exposure surface, so actionId is null where a governed surface has no action behind it. Covers declarative [Authorize] attributes only — imperative checks written inside method bodies are invisible to a syntax-derived graph, so treat this as a lower bound, not a complete authorization audit.")]
    public Task<string> GetGovernedActions(
        [Description("Exact Role or Policy node id.")] string roleOrPolicyId)
        => RunAsync("GetGovernedActions", () => graph.GetGovernedActionsAsync(roleOrPolicyId));

    [McpServerTool(Name = "FindStructurallySimilarActions"), Description("Returns actions whose outgoing edge-shape overlaps the given action's, ranked by overlap, as a starting point for 'has someone already built this shape here'. This is a heuristic structural proxy, not an authoritative pattern or archetype match: read the candidates before reusing them, and do not present the ranking to a user as a design judgement.")]
    public Task<string> FindStructurallySimilarActions(
        [Description("Exact Action node id.")] string actionId,
        [Description("Maximum candidates to return, clamped to 1..25.")] int limit = 10)
        => RunAsync("FindStructurallySimilarActions", () => graph.FindStructurallySimilarActionsAsync(actionId, limit));

    /// <summary>
    /// One guard for every tool body. Domain failures keep their own message — an ambiguous id is
    /// a question the caller can fix, and hiding it behind a connectivity hint sends them to the
    /// wrong problem. Anything else is reported as infrastructure, with the bring-up commands.
    /// </summary>
    private static async Task<string> RunAsync<T>(string tool, Func<Task<T>> action)
    {
        try
        {
            return Serialize(new ToolResult(tool, await action()));
        }
        catch (Exception exception) when (exception is AmbiguousNodeIdException or UnsupportedNodeLabelException)
        {
            Console.Error.WriteLine($"[kg-mcp] {tool}: {exception.Message}");
            return Serialize(new ToolError(tool, exception.Message));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[kg-mcp] {tool}: {exception.Message}");
            return Serialize(new ToolError(
                tool,
                $"Could not query the knowledge graph: {exception.Message}",
                "Check KG_NEO4J_URL, then run: docker compose --profile kg up -d neo4j  followed by  ./tools/kg/load-graph.ps1"));
        }
    }

    private static string Serialize(object payload)
    {
        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };
}
