using System.ComponentModel;
using System.Text.Json;
using KgMcp.Core;
using ModelContextProtocol.Server;

namespace KgMcp.Server;

[McpServerToolType]
public sealed class KgTools(KgGraphService graph)
{
    [McpServerTool(Name = "GetNodeNeighbors"), Description("Returns all incoming and outgoing edges for a node. Use this for a structural connection map; ambiguous ids return an error rather than an arbitrary node.")]
    public Task<string> GetNodeNeighbors(string nodeId) => RunAsync("GetNodeNeighbors", () => graph.QueryAsync("MATCH (n {id: $nodeId})-[r]-(m) RETURN n.id AS nodeId, labels(n)[0] AS nodeLabel, type(r) AS edgeType, startNode(r).id AS fromId, endNode(r).id AS toId, m.id AS neighborId, labels(m)[0] AS neighborLabel", new Dictionary<string, object?> { ["nodeId"] = nodeId }));

    [McpServerTool(Name = "GetBlastRadius"), Description("Returns a bounded forward structural blast radius from a node. maxDepth is clamped to 1..5; this is a graph reachability result, not an effort or time estimate.")]
    public Task<string> GetBlastRadius(string nodeId, int maxDepth = 3) => RunAsync("GetBlastRadius", () => graph.QueryAsync("MATCH (n {id: $nodeId})-[r*1..5]->(m) WHERE length(r) <= $maxDepth RETURN DISTINCT m.id AS nodeId, labels(m)[0] AS label, length(r) AS depth", new Dictionary<string, object?> { ["nodeId"] = nodeId, ["maxDepth"] = Math.Clamp(maxDepth, 1, 5) }));

    [McpServerTool(Name = "GetNodeDependencies"), Description("Returns structural prerequisites of a node by traversing dependency edges in reverse. Results are graph dependencies only, not runtime or test coverage claims.")]
    public Task<string> GetNodeDependencies(string nodeId) => RunAsync("GetNodeDependencies", () => graph.QueryAsync("MATCH (n {id: $nodeId})<-[r*1..5]-(m) RETURN DISTINCT m.id AS nodeId, labels(m)[0] AS label, [edge IN r | type(edge)] AS edgeTypes", new Dictionary<string, object?> { ["nodeId"] = nodeId }));

    [McpServerTool(Name = "GetModuleDependencies"), Description("Returns module-to-module integration paths through actions, messages, handlers, and queries. It does not invent a direct Module dependency edge.")]
    public Task<string> GetModuleDependencies(string moduleId) => RunAsync("GetModuleDependencies", () => graph.QueryAsync("MATCH (source:Module {id: $moduleId})-[:CONTAINS]->(a:Action)-[:PUBLISHES]->(m:Message)-[:HANDLED_BY]->(h:MessageHandler)-[:CONTAINS]-(target:Module) RETURN DISTINCT target.id AS moduleId, m.id AS contractId", new Dictionary<string, object?> { ["moduleId"] = moduleId }));

    [McpServerTool(Name = "GetModuleOwnership"), Description("Returns the entities and repositories contained by a module, identifying the graph source of truth for that bounded context.")]
    public Task<string> GetModuleOwnership(string moduleId) => RunAsync("GetModuleOwnership", () => graph.QueryAsync("MATCH (m:Module {id: $moduleId})-[:CONTAINS]->(n) RETURN n.id AS id, labels(n)[0] AS label", new Dictionary<string, object?> { ["moduleId"] = moduleId }));

    [McpServerTool(Name = "GetActionExposure"), Description("Returns endpoints and pages exposing an Action, or schedulers for a Job. The traversal branches on the resolved node label.")]
    public Task<string> GetActionExposure(string nodeId) => RunAsync("GetActionExposure", () => graph.QueryAsync("MATCH (n {id: $nodeId}) WHERE n:Action OR n:Job MATCH (caller)-[r]->(n) WHERE (n:Action AND type(r) = 'EXPOSED_BY') OR (n:Job AND type(r) = 'SCHEDULES') RETURN labels(n)[0] AS targetLabel, n.id AS targetId, type(r) AS edgeType, caller.id AS callerId, labels(caller)[0] AS callerLabel", new Dictionary<string, object?> { ["nodeId"] = nodeId }));

    [McpServerTool(Name = "GetOrphanContracts"), Description("Returns Message, Query, and Job contracts without their expected structural edges, with confidence and reasons. Jobs with no static scheduler may be legitimate runtime Scheduled or Manual jobs.")]
    public Task<string> GetOrphanContracts() => RunAsync("GetOrphanContracts", () => graph.QueryAsync("MATCH (n) WHERE n:Message OR n:Query OR n:Job OPTIONAL MATCH (caller)-[r]->(n) WITH n, collect(r) AS incoming OPTIONAL MATCH (n)-[out]->() WITH n, incoming, collect(out) AS outgoing WHERE size(incoming) = 0 AND size(outgoing) = 0 RETURN n.id AS id, labels(n)[0] AS label, CASE WHEN labels(n)[0] = 'Job' AND n.triggerMode IS NULL THEN 'ambiguous' WHEN labels(n)[0] = 'Job' THEN 'contradiction' ELSE 'high' END AS confidence, CASE WHEN labels(n)[0] = 'Job' AND n.triggerMode IS NULL THEN 'No static scheduler; verify runtime ScheduledJob table manually' WHEN labels(n)[0] = 'Job' THEN 'Deferred trigger has no SCHEDULES edge' ELSE 'No structural caller or handler edge' END AS reason"));

    [McpServerTool(Name = "GetJobSchedulers"), Description("Returns the static schedulers and trigger mode for a Job. Runtime ScheduledJob rows are outside the static graph.")]
    public Task<string> GetJobSchedulers(string jobId) => RunAsync("GetJobSchedulers", () => graph.QueryAsync("MATCH (j:Job {id: $jobId}) OPTIONAL MATCH (caller)-[:SCHEDULES]->(j) RETURN j.id AS jobId, j.triggerMode AS triggerMode, collect(caller.id) AS schedulerIds", new Dictionary<string, object?> { ["jobId"] = jobId }));

    [McpServerTool(Name = "GetGovernedActions"), Description("Returns actions governed by a Role or Policy through reverse GOVERNED_BY edges. It does not claim test coverage or authorization completeness.")]
    public Task<string> GetGovernedActions(string roleOrPolicyId) => RunAsync("GetGovernedActions", () => graph.QueryAsync("MATCH (a:Action)-[:GOVERNED_BY]->(g {id: $id}) RETURN a.id AS actionId, labels(g)[0] AS governorLabel, g.id AS governorId", new Dictionary<string, object?> { ["id"] = roleOrPolicyId }));

    [McpServerTool(Name = "FindStructurallySimilarActions"), Description("Returns Actions with overlapping edge-shape signatures. This is a heuristic structural proxy for reuse research, not an authoritative pattern or archetype match.")]
    public Task<string> FindStructurallySimilarActions(string actionId) => RunAsync("FindStructurallySimilarActions", () => graph.QueryAsync("MATCH (a:Action {id: $id})-[r]->() WITH a, collect(DISTINCT type(r)) AS shape MATCH (candidate:Action)-[cr]->() WHERE candidate <> a WITH candidate, shape, collect(DISTINCT type(cr)) AS candidateShape WHERE any(edgeType IN shape WHERE edgeType IN candidateShape) RETURN candidate.id AS actionId, candidateShape", new Dictionary<string, object?> { ["id"] = actionId }));

    private static async Task<string> RunAsync(string tool, Func<Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>>> action)
    {
        try
        {
            return JsonSerializer.Serialize(new ToolResult(tool, await action()));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[kg-mcp] {tool}: {exception.Message}");
            return JsonSerializer.Serialize(new { error = $"{tool} failed. Check KG_NEO4J_URL and run docker compose --profile kg up -d neo4j followed by tools/kg/load-graph.ps1." });
        }
    }
}
