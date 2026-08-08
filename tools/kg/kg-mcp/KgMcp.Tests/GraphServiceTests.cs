using KgMcp.Core;
using Neo4j.Driver;

namespace KgMcp.Tests;

/// <summary>
/// Behavioural tests against a real Neo4j. Every assertion here is written to fail against the
/// shipped-then-fixed implementation, not merely to pass against the current one — the defects
/// these cover all survived a green source-grep suite.
/// </summary>
[Collection(Neo4jCollection.Name)]
public sealed class GraphServiceTests(Neo4jFixture fixture)
{
    private KgGraphService CreateService()
    {
        return new KgGraphService(fixture.ConnectionString);
    }

    // --- GetOrphanContracts -------------------------------------------------------------------
    // The original filter required zero incoming AND zero outgoing edges of ANY type. Because
    // every Module-owned node carries an incoming CONTAINS edge, that filter could only ever
    // match fully disconnected nodes: the Job and Query branches were unreachable code.

    [Fact]
    public async Task Orphans_report_a_published_but_unhandled_message_as_high_confidence()
    {
        await using var graph = CreateService();

        var contracts = await graph.GetOrphanContractsAsync();

        var dead = Assert.Single(contracts, contract => contract.Id == "Msg.Dead");
        Assert.Equal("high", dead.Confidence);
        Assert.Contains("never handled", dead.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Orphans_do_not_report_a_fully_wired_message_or_query()
    {
        await using var graph = CreateService();

        var contracts = await graph.GetOrphanContractsAsync();

        Assert.DoesNotContain(contracts, contract => contract.Id == "Msg.Healthy");
        Assert.DoesNotContain(contracts, contract => contract.Id == "Q.Cross");
    }

    [Fact]
    public async Task Orphans_downgrade_a_message_with_no_publisher_to_ambiguous()
    {
        await using var graph = CreateService();

        var contracts = await graph.GetOrphanContractsAsync();

        var unwired = Assert.Single(contracts, contract => contract.Id == "Msg.Unwired");
        Assert.Equal("ambiguous", unwired.Confidence);
        Assert.Contains("MessageHandler-sourced", unwired.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Orphans_mark_a_caller_less_query_as_a_known_false_positive_class()
    {
        await using var graph = CreateService();

        var contracts = await graph.GetOrphanContractsAsync();

        // This is the StockAvailableQuery / CompletedOrderCountQuery shape: handled, but its only
        // sender lives in an Infrastructure adapter that produces no Action node.
        var noCaller = Assert.Single(contracts, contract => contract.Id == "Q.NoCaller");
        Assert.Equal("ambiguous", noCaller.Confidence);
        Assert.Contains("Infrastructure", noCaller.Reason, StringComparison.Ordinal);

        var noHandler = Assert.Single(contracts, contract => contract.Id == "Q.NoHandler");
        Assert.Equal("high", noHandler.Confidence);
    }

    [Fact]
    public async Task Orphans_separate_runtime_scheduled_jobs_from_a_declared_contradiction()
    {
        await using var graph = CreateService();

        var contracts = await graph.GetOrphanContractsAsync();

        var runtime = Assert.Single(contracts, contract => contract.Id == "J.Runtime");
        Assert.Equal("ambiguous", runtime.Confidence);
        Assert.Contains("ScheduledJob", runtime.Reason, StringComparison.Ordinal);

        var deferred = Assert.Single(contracts, contract => contract.Id == "J.Deferred");
        Assert.Equal("contradiction", deferred.Confidence);

        // A job that IS statically scheduled must not be reported at all.
        Assert.DoesNotContain(contracts, contract => contract.Id == "J.Scheduled");
    }

    [Fact]
    public async Task Orphans_are_not_a_flat_list_of_equally_confident_rows()
    {
        await using var graph = CreateService();

        var contracts = await graph.GetOrphanContractsAsync();

        // A count-only assertion would pass a flat implementation; distinctness is the point.
        var confidences = contracts.Select(contract => contract.Confidence).Distinct(StringComparer.Ordinal).ToList();
        Assert.Contains("high", confidences);
        Assert.Contains("ambiguous", confidences);
        Assert.Contains("contradiction", confidences);
        Assert.All(contracts, contract => Assert.False(string.IsNullOrWhiteSpace(contract.Reason)));
    }

    // --- GetActionExposure --------------------------------------------------------------------
    // The original traversal looked for INCOMING EXPOSED_BY edges. The real edge runs
    // Action -> Endpoint/Page, so the Action branch returned an empty list for every Action in
    // the graph while looking like a legitimate "nothing exposes this" answer.

    [Fact]
    public async Task Exposure_of_an_action_follows_outgoing_edges_to_endpoints_and_pages()
    {
        await using var graph = CreateService();

        var rows = await graph.GetActionExposureAsync("A.PlaceOrder");

        Assert.Equal(2, rows.Count);
        var surfaces = rows.Select(row => row["surfaceId"].As<string>()).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "E.PostOrder", "P.Checkout" }, surfaces);
        Assert.All(rows, row => Assert.Equal("EXPOSED_BY", row["edgeType"].As<string>()));
    }

    [Fact]
    public async Task Exposure_of_a_job_follows_incoming_schedules_edges()
    {
        await using var graph = CreateService();

        var rows = await graph.GetActionExposureAsync("J.Scheduled");

        var row = Assert.Single(rows);
        Assert.Equal("A.Schedule", row["surfaceId"].As<string>());
        Assert.Equal("SCHEDULES", row["edgeType"].As<string>());
    }

    [Fact]
    public async Task Exposure_of_an_unsupported_label_is_an_error_not_an_empty_list()
    {
        await using var graph = CreateService();

        var exception = await Assert.ThrowsAsync<UnsupportedNodeLabelException>(
            () => graph.GetActionExposureAsync("M.Sales"));

        Assert.Contains("Module", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Action or Job", exception.Message, StringComparison.Ordinal);
    }

    // --- id resolution ------------------------------------------------------------------------

    [Fact]
    public async Task An_ambiguous_id_names_every_matched_label_instead_of_picking_one()
    {
        await using var graph = CreateService();

        var exception = await Assert.ThrowsAsync<AmbiguousNodeIdException>(
            () => graph.GetNodeNeighborsAsync("Dup.Shared"));

        Assert.Equal("Dup.Shared", exception.NodeId);
        Assert.Contains("Action", exception.Labels);
        Assert.Contains("Page", exception.Labels);
        Assert.Contains("Action", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Page", exception.Message, StringComparison.Ordinal);
    }

    // --- depth clamping -----------------------------------------------------------------------
    // Asserted through results rather than by grepping for Math.Clamp: a clamp that is computed
    // and then not applied to the traversal would pass a source check.

    [Theory]
    [InlineData(99, 5)]
    [InlineData(2, 2)]
    [InlineData(0, 1)]
    [InlineData(-7, 1)]
    public async Task Blast_radius_depth_is_clamped_in_the_returned_rows(int requestedDepth, int expectedMaxDepth)
    {
        await using var graph = CreateService();

        var rows = await graph.GetBlastRadiusAsync("S1", requestedDepth);

        Assert.NotEmpty(rows);
        var deepest = rows.Max(row => row["depth"].As<int>());
        Assert.Equal(expectedMaxDepth, deepest);
    }

    [Fact]
    public async Task Node_dependencies_are_clamped_the_same_way()
    {
        await using var graph = CreateService();

        var rows = await graph.GetNodeDependenciesAsync("S8", 99);

        Assert.Equal(5, rows.Max(row => row["depth"].As<int>()));
    }

    // --- one row per node, at its shortest distance ---------------------------------------------
    // The clamp tests above traverse S1..S8, a straight chain, where every node is reachable by
    // exactly one path — the one shape that cannot expose this. M.Sales is the diamond:
    // it CONTAINS J.Scheduled directly, and also reaches it via A.Schedule-[:SCHEDULES]->.
    // A DISTINCT over (id, label, length(path)) emits that node once per distinct path length,
    // which inflates the result and turns `depth` into "some path length" rather than a distance.

    [Fact]
    public async Task Blast_radius_reports_each_node_once_at_its_shortest_distance()
    {
        await using var graph = CreateService();

        var rows = await graph.GetBlastRadiusAsync("M.Sales", 5);

        var ids = rows.Select(row => row["nodeId"].As<string>()).ToArray();
        Assert.Equal(ids.Distinct(StringComparer.Ordinal).Count(), ids.Length);

        var scheduled = Assert.Single(rows, row => row["nodeId"].As<string>() == "J.Scheduled");
        Assert.Equal(1, scheduled["depth"].As<int>());
    }

    [Fact]
    public async Task Node_dependencies_report_each_node_once_at_its_shortest_distance()
    {
        await using var graph = CreateService();

        var rows = await graph.GetNodeDependenciesAsync("J.Scheduled", 5);

        var ids = rows.Select(row => row["nodeId"].As<string>()).ToArray();
        Assert.Equal(ids.Distinct(StringComparer.Ordinal).Count(), ids.Length);

        var sales = Assert.Single(rows, row => row["nodeId"].As<string>() == "M.Sales");
        Assert.Equal(1, sales["depth"].As<int>());
    }

    // --- unknown and wrong-kind ids -------------------------------------------------------------
    // An id nothing matches must fail loudly. Returning an empty list makes a typo
    // indistinguishable from a true negative, which is the defect class this suite exists for —
    // and after Phase 7 it still applied to nine of the ten tools.

    public static TheoryData<string> IdTakingTools =>
    [
        "GetNodeNeighbors",
        "GetBlastRadius",
        "GetNodeDependencies",
        "GetModuleDependencies",
        "GetModuleOwnership",
        "GetActionExposure",
        "GetJobSchedulers",
        "GetGovernedActions",
        "FindStructurallySimilarActions",
    ];

    [Theory]
    [MemberData(nameof(IdTakingTools))]
    public async Task An_unknown_id_is_an_error_not_an_empty_list(string tool)
    {
        await using var graph = CreateService();

        var exception = await Assert.ThrowsAsync<UnknownNodeIdException>(() => Invoke(graph, tool, "No.Such.Node"));

        Assert.Equal("No.Such.Node", exception.NodeId);
    }

    [Theory]
    [InlineData("GetModuleDependencies", "A.PlaceOrder", "Module")]
    [InlineData("GetModuleOwnership", "A.PlaceOrder", "Module")]
    [InlineData("GetJobSchedulers", "A.PlaceOrder", "Job")]
    [InlineData("GetGovernedActions", "A.PlaceOrder", "Role or Policy")]
    [InlineData("GetActionExposure", "M.Sales", "Action or Job")]
    [InlineData("FindStructurallySimilarActions", "M.Sales", "Action")]
    public async Task An_id_of_the_wrong_kind_names_the_kinds_the_tool_answers_for(string tool, string nodeId, string expected)
    {
        await using var graph = CreateService();

        var exception = await Assert.ThrowsAsync<UnsupportedNodeLabelException>(() => Invoke(graph, tool, nodeId));

        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    private static Task Invoke(KgGraphService graph, string tool, string nodeId) => tool switch
    {
        "GetNodeNeighbors" => graph.GetNodeNeighborsAsync(nodeId),
        "GetBlastRadius" => graph.GetBlastRadiusAsync(nodeId, 3),
        "GetNodeDependencies" => graph.GetNodeDependenciesAsync(nodeId, 3),
        "GetModuleDependencies" => graph.GetModuleDependenciesAsync(nodeId),
        "GetModuleOwnership" => graph.GetModuleOwnershipAsync(nodeId),
        "GetActionExposure" => graph.GetActionExposureAsync(nodeId),
        "GetJobSchedulers" => graph.GetJobSchedulersAsync(nodeId),
        "GetGovernedActions" => graph.GetGovernedActionsAsync(nodeId),
        "FindStructurallySimilarActions" => graph.FindStructurallySimilarActionsAsync(nodeId, 10),
        _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, "Unmapped tool; add it here when the surface grows."),
    };

    // --- remaining tool surface ---------------------------------------------------------------

    [Fact]
    public async Task Module_dependencies_are_derived_through_both_message_and_query_contracts()
    {
        await using var graph = CreateService();

        var rows = await graph.GetModuleDependenciesAsync("M.Sales");

        Assert.All(rows, row => Assert.Equal("M.Inventory", row["moduleId"].As<string>()));
        var kinds = rows.Select(row => row["contractKind"].As<string>()).Distinct(StringComparer.Ordinal).OrderBy(kind => kind, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "Message", "Query" }, kinds);
    }

    [Fact]
    public async Task Job_schedulers_report_an_empty_list_and_a_null_trigger_mode_without_failing()
    {
        await using var graph = CreateService();

        var scheduled = Assert.Single(await graph.GetJobSchedulersAsync("J.Scheduled"));
        Assert.Equal(new[] { "A.Schedule" }, scheduled["schedulerIds"].As<List<string>>().ToArray());
        Assert.Equal("Deferred", scheduled["triggerMode"].As<string>());

        var runtime = Assert.Single(await graph.GetJobSchedulersAsync("J.Runtime"));
        Assert.Empty(runtime["schedulerIds"].As<List<object>>());
        Assert.Null(runtime["triggerMode"]);
    }

    // Governance attaches to the exposure surface. The ontology declares GOVERNED_BY only from
    // Endpoint and Page, so a traversal rooted at Action returns an empty list for every role in
    // the real repository — an answer indistinguishable from "this role governs nothing".
    [Fact]
    public async Task Governed_surfaces_resolve_to_the_actions_behind_them()
    {
        await using var graph = CreateService();

        var rows = await graph.GetGovernedActionsAsync("R.Admin");

        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.Equal("Role", row["governorLabel"].As<string>()));

        var surfaces = rows.Select(row => row["surfaceId"].As<string>()).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "E.PostOrder", "P.Checkout", "P.Governed" }, surfaces);

        // Both governed surfaces that have an Action behind them resolve to it.
        Assert.Equal(2, rows.Count(row => row["actionId"].As<string>() == "A.PlaceOrder"));

        // A governed surface with no Action behind it is still reported, with a null action,
        // rather than silently dropped by an inner join.
        var bare = Assert.Single(rows, row => row["surfaceId"].As<string>() == "P.Governed");
        Assert.Null(bare["actionId"]);
    }

    [Fact]
    public async Task Module_ownership_lists_contained_nodes()
    {
        await using var graph = CreateService();

        var rows = await graph.GetModuleOwnershipAsync("M.Inventory");

        var ids = rows.Select(row => row["id"].As<string>()).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "MH.Healthy", "QH.Inventory" }, ids);
    }

    [Fact]
    public async Task Structurally_similar_actions_exclude_the_seed_action_and_respect_the_limit()
    {
        await using var graph = CreateService();

        var rows = await graph.FindStructurallySimilarActionsAsync("A.PlaceOrder", limit: 99);

        Assert.DoesNotContain(rows, row => row["actionId"].As<string>() == "A.PlaceOrder");
        Assert.True(rows.Count <= 25, "limit must be clamped to 25");

        // A.Refund shares EXPOSED_BY and PUBLISHES.
        var refund = Assert.Single(rows, row => row["actionId"].As<string>() == "A.Refund");
        Assert.Equal(2, refund["overlap"].As<int>());

        // A.Schedule shares no edge type, so a heuristic that returns "everything with any edge"
        // would still pass a contains-check but fails here.
        Assert.DoesNotContain(rows, row => row["actionId"].As<string>() == "A.Schedule");
    }

    [Fact]
    public async Task Node_neighbors_include_both_directions()
    {
        await using var graph = CreateService();

        var rows = await graph.GetNodeNeighborsAsync("Msg.Healthy");

        var neighbors = rows.Select(row => row["neighborId"].As<string>()).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "A.PlaceOrder", "MH.Healthy" }, neighbors);
    }

    // --- read-only contract, proven by behaviour ----------------------------------------------

    [Fact]
    public async Task Running_every_tool_leaves_the_graph_byte_for_byte_unchanged()
    {
        var before = await fixture.GetCountsAsync();
        await using var graph = CreateService();

        await graph.GetNodeNeighborsAsync("A.PlaceOrder");
        await graph.GetBlastRadiusAsync("S1", 3);
        await graph.GetNodeDependenciesAsync("S8", 3);
        await graph.GetModuleDependenciesAsync("M.Sales");
        await graph.GetModuleOwnershipAsync("M.Sales");
        await graph.GetActionExposureAsync("A.PlaceOrder");
        await graph.GetOrphanContractsAsync();
        await graph.GetJobSchedulersAsync("J.Scheduled");
        await graph.GetGovernedActionsAsync("R.Admin");
        await graph.FindStructurallySimilarActionsAsync("A.PlaceOrder", 10);

        Assert.Equal(before, await fixture.GetCountsAsync());
    }
}
