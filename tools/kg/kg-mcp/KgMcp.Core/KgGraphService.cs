using Neo4j.Driver;

namespace KgMcp.Core;

/// <summary>Raised when a node id matches more than one node. Distinct from an infrastructure
/// failure so the tool layer can surface the real cause instead of a generic connectivity hint.</summary>
public sealed class AmbiguousNodeIdException(string nodeId, IReadOnlyList<string> labels)
    : InvalidOperationException($"Ambiguous node id '{nodeId}': it matches {labels.Count} nodes across labels [{string.Join(", ", labels)}]. Query the intended label directly instead.")
{
    public string NodeId { get; } = nodeId;

    public IReadOnlyList<string> Labels { get; } = labels;
}

/// <summary>Raised when a node exists but is not a kind this tool can answer for.</summary>
public sealed class UnsupportedNodeLabelException(string nodeId, string? label, string expected)
    : InvalidOperationException($"Node '{nodeId}' has label '{label ?? "<none>"}', but this tool answers only for {expected}.")
{
}

/// <summary>
/// Every graph traversal lives here. The MCP layer is a delegation shell on purpose: keeping
/// Cypher out of the tool attributes is what lets these traversals be tested against a real
/// database without an MCP host in the loop.
/// </summary>
public sealed class KgGraphService(string uri, IAuthToken? authToken = null) : IAsyncDisposable
{
    private readonly IDriver _driver = GraphDatabase.Driver(uri, authToken ?? AuthTokens.None);

    /// <summary>All edges touching a node, in both directions.</summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> GetNodeNeighborsAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        await ResolveLabelAsync(nodeId, cancellationToken);
        return await QueryAsync(
            """
            MATCH (n {id: $nodeId})-[r]-(m)
            RETURN n.id AS nodeId, labels(n)[0] AS nodeLabel, type(r) AS edgeType,
                   startNode(r).id AS fromId, endNode(r).id AS toId,
                   m.id AS neighborId, labels(m)[0] AS neighborLabel
            ORDER BY edgeType, neighborId
            """,
            new Dictionary<string, object?> { ["nodeId"] = nodeId },
            cancellationToken);
    }

    /// <summary>Forward reachability, depth-bounded. The clamp is applied here, not in the caller,
    /// so no transport can widen it.</summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> GetBlastRadiusAsync(string nodeId, int maxDepth, CancellationToken cancellationToken = default)
    {
        await ResolveLabelAsync(nodeId, cancellationToken);
        var depth = Math.Clamp(maxDepth, MinDepth, MaxDepth);
        return await QueryAsync(
            $$"""
            MATCH path = (n {id: $nodeId})-[*1..{{depth}}]->(m)
            RETURN DISTINCT m.id AS nodeId, labels(m)[0] AS label, length(path) AS depth
            ORDER BY depth, nodeId
            """,
            new Dictionary<string, object?> { ["nodeId"] = nodeId },
            cancellationToken);
    }

    /// <summary>Structural prerequisites: what must exist for this node to work, by reverse traversal.</summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> GetNodeDependenciesAsync(string nodeId, int maxDepth, CancellationToken cancellationToken = default)
    {
        await ResolveLabelAsync(nodeId, cancellationToken);
        var depth = Math.Clamp(maxDepth, MinDepth, MaxDepth);
        return await QueryAsync(
            $$"""
            MATCH path = (n {id: $nodeId})<-[*1..{{depth}}]-(m)
            RETURN DISTINCT m.id AS nodeId, labels(m)[0] AS label, length(path) AS depth
            ORDER BY depth, nodeId
            """,
            new Dictionary<string, object?> { ["nodeId"] = nodeId },
            cancellationToken);
    }

    /// <summary>Module-to-module integration paths. There is no direct Module dependency edge in
    /// the ontology; these are derived through the contracts that actually cross the boundary.</summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> GetModuleDependenciesAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        return await QueryAsync(
            """
            MATCH (source:Module {id: $moduleId})-[:CONTAINS]->(a:Action)-[:PUBLISHES]->(m:Message)-[:HANDLED_BY]->(h:MessageHandler)
            MATCH (target:Module)-[:CONTAINS]->(h)
            WHERE target <> source
            RETURN DISTINCT target.id AS moduleId, m.id AS contractId, 'Message' AS contractKind
            UNION
            MATCH (source:Module {id: $moduleId})-[:CONTAINS]->(a:Action)-[:USES]->(q:Query)-[:HANDLED_BY]->(qh:QueryHandler)
            MATCH (target:Module)-[:CONTAINS]->(qh)
            WHERE target <> source
            RETURN DISTINCT target.id AS moduleId, q.id AS contractId, 'Query' AS contractKind
            """,
            new Dictionary<string, object?> { ["moduleId"] = moduleId },
            cancellationToken);
    }

    /// <summary>What a module owns.</summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> GetModuleOwnershipAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        return await QueryAsync(
            """
            MATCH (m:Module {id: $moduleId})-[:CONTAINS]->(n)
            RETURN n.id AS id, labels(n)[0] AS label
            ORDER BY label, id
            """,
            new Dictionary<string, object?> { ["moduleId"] = moduleId },
            cancellationToken);
    }

    /// <summary>
    /// How a node is reached from outside. Branches on the resolved label: an Action is exposed
    /// through outgoing EXPOSED_BY edges, a Job is reached through incoming SCHEDULES edges.
    /// Running one traversal for both returns an empty list for half the inputs, which reads to a
    /// caller as "nothing exposes this" rather than "wrong question".
    /// </summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> GetActionExposureAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var label = await ResolveLabelAsync(nodeId, cancellationToken);
        return label switch
        {
            "Action" => await QueryAsync(
                """
                MATCH (a:Action {id: $nodeId})-[r:EXPOSED_BY]->(surface)
                RETURN a.id AS targetId, 'Action' AS targetLabel, type(r) AS edgeType,
                       surface.id AS surfaceId, labels(surface)[0] AS surfaceLabel
                ORDER BY surfaceLabel, surfaceId
                """,
                new Dictionary<string, object?> { ["nodeId"] = nodeId },
                cancellationToken),
            "Job" => await QueryAsync(
                """
                MATCH (scheduler)-[r:SCHEDULES]->(j:Job {id: $nodeId})
                RETURN j.id AS targetId, 'Job' AS targetLabel, type(r) AS edgeType,
                       scheduler.id AS surfaceId, labels(scheduler)[0] AS surfaceLabel
                ORDER BY surfaceLabel, surfaceId
                """,
                new Dictionary<string, object?> { ["nodeId"] = nodeId },
                cancellationToken),
            _ => throw new UnsupportedNodeLabelException(nodeId, label, "Action or Job"),
        };
    }

    /// <summary>
    /// Contracts missing an edge the ontology leads a reader to expect. Each row carries its own
    /// confidence because several classes here are known, explainable false positives — reporting
    /// them as a flat list would let a caller delete live code.
    /// </summary>
    public async Task<IReadOnlyList<OrphanContract>> GetOrphanContractsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync(
            """
            MATCH (m:Message)
            OPTIONAL MATCH (publisher)-[:PUBLISHES]->(m)
            OPTIONAL MATCH (m)-[:HANDLED_BY]->(handler)
            WITH m, count(DISTINCT publisher) AS publishers, count(DISTINCT handler) AS handlers
            WHERE publishers = 0 OR handlers = 0
            RETURN m.id AS id, 'Message' AS label,
                   CASE WHEN handlers = 0 AND publishers > 0 THEN 'high' ELSE 'ambiguous' END AS confidence,
                   CASE
                     WHEN handlers = 0 AND publishers > 0
                       THEN 'Published but never handled: no HANDLED_BY edge. Strongest dead-contract signal in the graph.'
                     WHEN handlers = 0 AND publishers = 0
                       THEN 'Neither published nor handled. MessageHandler-sourced publishes are unrepresentable in this ontology, so a message enqueued only from a handler looks unpublished here.'
                     ELSE 'Handled but no static publisher. MessageHandler-sourced publishes are unrepresentable in this ontology.'
                   END AS reason
            UNION ALL
            MATCH (q:Query)
            OPTIONAL MATCH (caller)-[:USES]->(q)
            OPTIONAL MATCH (q)-[:HANDLED_BY]->(queryHandler)
            WITH q, count(DISTINCT caller) AS callers, count(DISTINCT queryHandler) AS handlers
            WHERE callers = 0 OR handlers = 0
            RETURN q.id AS id, 'Query' AS label,
                   CASE WHEN handlers = 0 THEN 'high' ELSE 'ambiguous' END AS confidence,
                   CASE
                     WHEN handlers = 0
                       THEN 'No QueryHandler handles this query.'
                     ELSE 'No Action-sourced USES edge. Senders living under Infrastructure adapters produce no Action node, a known false-positive class.'
                   END AS reason
            UNION ALL
            MATCH (j:Job)
            OPTIONAL MATCH (scheduler)-[:SCHEDULES]->(j)
            WITH j, count(DISTINCT scheduler) AS schedulers
            WHERE schedulers = 0
            RETURN j.id AS id, 'Job' AS label,
                   CASE WHEN j.triggerMode = 'Deferred' THEN 'contradiction' ELSE 'ambiguous' END AS confidence,
                   CASE
                     WHEN j.triggerMode = 'Deferred'
                       THEN 'Trigger mode is Deferred, which requires a static SCHEDULES edge, but none exists.'
                     ELSE 'No static scheduler. Scheduled and Manual triggers live in the runtime ScheduledJob table and are invisible to a static graph; verify there before treating this as dead.'
                   END AS reason
            """,
            parameters: null,
            cancellationToken);

        return rows
            .Select(row => new OrphanContract(
                row["id"].As<string>(),
                row["label"].As<string>(),
                row["confidence"].As<string>(),
                row["reason"].As<string>()))
            .OrderBy(contract => contract.Label, StringComparer.Ordinal)
            .ThenBy(contract => contract.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Static schedulers for a Job, plus its trigger mode.</summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> GetJobSchedulersAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return await QueryAsync(
            """
            MATCH (j:Job {id: $jobId})
            OPTIONAL MATCH (scheduler)-[:SCHEDULES]->(j)
            RETURN j.id AS jobId, j.triggerMode AS triggerMode,
                   [s IN collect(scheduler) WHERE s IS NOT NULL | s.id] AS schedulerIds
            """,
            new Dictionary<string, object?> { ["jobId"] = jobId },
            cancellationToken);
    }

    /// <summary>
    /// What a Role or Policy governs.
    ///
    /// Authorization attaches to the exposure surface, not to the Action: the ontology declares
    /// GOVERNED_BY only from Endpoint and Page. Traversing Action-[:GOVERNED_BY]-> instead asks
    /// for a triple that cannot exist, and returns an empty list for every role in the repository.
    /// The Action behind each surface is derived through EXPOSED_BY, and is null for a governed
    /// surface that has no action behind it.
    /// </summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> GetGovernedActionsAsync(string roleOrPolicyId, CancellationToken cancellationToken = default)
    {
        return await QueryAsync(
            """
            MATCH (surface)-[:GOVERNED_BY]->(governor {id: $id})
            WHERE surface:Endpoint OR surface:Page
            OPTIONAL MATCH (action:Action)-[:EXPOSED_BY]->(surface)
            RETURN DISTINCT action.id AS actionId, surface.id AS surfaceId,
                   labels(surface)[0] AS surfaceLabel,
                   labels(governor)[0] AS governorLabel, governor.id AS governorId
            ORDER BY surfaceLabel, surfaceId, actionId
            """,
            new Dictionary<string, object?> { ["id"] = roleOrPolicyId },
            cancellationToken);
    }

    /// <summary>
    /// Actions whose outgoing edge-type signature overlaps this one's, ranked by overlap size.
    /// A structural proxy for "has someone solved this shape before" — not a pattern match.
    /// </summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> FindStructurallySimilarActionsAsync(string actionId, int limit, CancellationToken cancellationToken = default)
    {
        await ResolveLabelAsync(actionId, cancellationToken);
        return await QueryAsync(
            """
            MATCH (a:Action {id: $id})-[r]->()
            WITH a, collect(DISTINCT type(r)) AS shape
            MATCH (candidate:Action)-[cr]->()
            WHERE candidate <> a
            WITH candidate, shape, collect(DISTINCT type(cr)) AS candidateShape
            WITH candidate, candidateShape,
                 size([edgeType IN shape WHERE edgeType IN candidateShape]) AS overlap,
                 size(shape) AS shapeSize
            WHERE overlap > 0
            RETURN candidate.id AS actionId, candidateShape, overlap,
                   toFloat(overlap) / shapeSize AS shapeCoverage
            ORDER BY overlap DESC, actionId
            LIMIT $limit
            """,
            new Dictionary<string, object?> { ["id"] = actionId, ["limit"] = Math.Clamp(limit, 1, MaxSimilarActions) },
            cancellationToken);
    }

    /// <summary>
    /// Single shared id resolver. Returns the node's label so callers that branch on it do not
    /// pay for a second round trip, and refuses ambiguous ids rather than picking one arbitrarily.
    /// </summary>
    public async Task<string?> ResolveLabelAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var matches = await QueryAsync(
            "MATCH (n {id: $nodeId}) RETURN labels(n)[0] AS label ORDER BY label",
            new Dictionary<string, object?> { ["nodeId"] = nodeId },
            cancellationToken);

        if (matches.Count > 1)
        {
            var labels = matches.Select(match => match["label"].As<string>()).ToList();
            throw new AmbiguousNodeIdException(nodeId, labels);
        }

        return matches.Count == 0 ? null : matches[0]["label"].As<string>();
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(string cypher, IDictionary<string, object?>? parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var session = _driver.AsyncSession(builder => builder.WithDefaultAccessMode(AccessMode.Read));
        return await session.ExecuteReadAsync(async transaction =>
        {
            var cursor = await transaction.RunAsync(cypher, parameters);
            var records = await cursor.ToListAsync();
            return records
                .Select(record => (IReadOnlyDictionary<string, object?>)record.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal))
                .ToList();
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _driver.DisposeAsync();
    }

    private const int MinDepth = 1;
    private const int MaxDepth = 5;
    private const int MaxSimilarActions = 25;
}
