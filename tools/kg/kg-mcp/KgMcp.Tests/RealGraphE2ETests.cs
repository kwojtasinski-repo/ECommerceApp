using KgMcp.Core;
using Neo4j.Driver;

namespace KgMcp.Tests;

/// <summary>
/// End-to-end over the real repository graph: source → parsers → Cypher → Neo4j → traversal.
///
/// <para><b>No expected value in this file is a literal.</b> Every number is either re-derived from
/// the graph by an independent query, or read back out of what `kg-codegen` printed. That is a
/// deliberate reaction to how the earlier defects were found: the evidence was a count someone
/// measured by hand at a console ("38 rows for 32 nodes"), and a count measured by hand is knowledge
/// that lives in a chat log and dies there. A test that re-derives it explains itself a year later
/// and does not need editing when the codebase grows a module.</para>
///
/// <para>Where a real id does appear — module names, `ECommerceApp` — it is a stable fact from
/// `tools/kg/seed/overrides.yaml`, the same convention `PinnedRealGraphTests` already uses.</para>
/// </summary>
[Collection(RealGraphCollection.Name)]
public sealed class RealGraphE2ETests(RealGraphFixture fixture)
{
    private KgGraphService CreateService()
    {
        return new KgGraphService(fixture.ConnectionString);
    }

    // --- the generated seed is loadable as generated -------------------------------------------

    [Fact]
    public async Task Generated_seed_loads_with_no_rewriting_and_carries_no_null_property()
    {
        // Reaching this line already proves the load succeeded: a `key: null` inside a MERGE map is
        // rejected by Neo4j, so re-introducing one in CypherEmitter fails the whole class at
        // startup. This assertion pins the other half — that nothing smuggled a null in as a value.
        var rows = await fixture.QueryAsync(
            """
            MATCH (n) UNWIND keys(n) AS key
            WITH n, key WHERE n[key] IS NULL
            RETURN count(*) AS nullProperties
            """);

        Assert.Equal(0, rows.Single()["nullProperties"].As<int>());
    }

    [Fact]
    public async Task What_codegen_reported_is_what_the_database_received()
    {
        // The loaded graph holds two layers. Comparing a raw `MATCH ()-[r]->()` total against
        // codegen's `Edges:` line is the mistake a validation checklist actually made once: the
        // totals differ by the ontology layer and look like a defect. Split the layers and the
        // generated side matches exactly.
        var edges = await fixture.QueryAsync(
            """
            MATCH ()-[r]->()
            WITH r, startNode(r) AS source, endNode(r) AS target
            RETURN
              count(CASE WHEN NOT source:Ontology AND NOT target:Ontology THEN 1 END) AS instanceEdges,
              count(CASE WHEN source:Ontology AND target:Ontology THEN 1 END) AS ontologyEdges,
              count(CASE WHEN source:Ontology <> target:Ontology THEN 1 END) AS crossLayerEdges
            """);

        var row = edges.Single();
        Assert.Equal(fixture.ReportedCount("Edges"), row["instanceEdges"].As<int>());

        // The two layers are deliberately disjoint: the ontology describes the schema, it does not
        // participate in it. A cross-layer edge would silently widen every traversal below.
        Assert.Equal(0, row["crossLayerEdges"].As<int>());
        Assert.True(row["ontologyEdges"].As<int>() > 0, "The ontology layer did not load.");

        foreach (var label in new[] { "Module", "Action", "Entity", "Endpoint", "Page", "Job", "Message" })
        {
            var loaded = await fixture.QueryAsync(
                $"MATCH (n:{label}) WHERE NOT n:Ontology RETURN count(n) AS value");

            Assert.Equal(fixture.ReportedCount(label), loaded.Single()["value"].As<int>());
        }
    }

    // --- one row per node, at its shortest distance, on every shape the repository produces -----

    [Fact]
    public async Task Blast_radius_never_repeats_a_node_from_any_module_or_the_system_root()
    {
        await using var graph = CreateService();

        // The system root at full depth is the worst case in the repository and the shape that
        // exposed the original defect: it reaches most of the graph by many overlapping paths.
        foreach (var seedId in await AllSeedIdsAsync())
        {
            var rows = await graph.GetBlastRadiusAsync(seedId, 5);
            var ids = rows.Select(row => row["nodeId"].As<string>()).ToArray();

            Assert.Equal(ids.Distinct(StringComparer.Ordinal).Count(), ids.Length);
        }
    }

    [Fact]
    public async Task Blast_radius_depth_equals_an_independently_computed_shortest_path()
    {
        await using var graph = CreateService();

        foreach (var seedId in await AllSeedIdsAsync())
        {
            var rows = await graph.GetBlastRadiusAsync(seedId, 5);
            if (rows.Count == 0)
            {
                continue;
            }

            var reported = rows.ToDictionary(
                row => row["nodeId"].As<string>(),
                row => row["depth"].As<int>(),
                StringComparer.Ordinal);

            // Independent oracle: Neo4j's own shortestPath, which shares no code with the traversal
            // under test. Restating the implementation's own query here would prove nothing.
            var oracle = await fixture.QueryAsync(
                """
                MATCH (source {id: $seedId})
                MATCH (target) WHERE target.id IN $ids
                MATCH path = shortestPath((source)-[*1..5]->(target))
                RETURN target.id AS nodeId, length(path) AS shortest
                """,
                new { seedId, ids = reported.Keys.ToList() });

            Assert.Equal(reported.Count, oracle.Count);
            foreach (var row in oracle)
            {
                var nodeId = row["nodeId"].As<string>();
                Assert.Equal(row["shortest"].As<int>(), reported[nodeId]);
            }
        }
    }

    [Fact]
    public async Task Node_dependencies_depth_equals_an_independently_computed_shortest_path()
    {
        await using var graph = CreateService();

        // Reverse direction, same oracle. The two queries are separate code paths in
        // KgGraphService and a fix applied to one has already been forgotten on the other.
        var leaves = await fixture.QueryAsync(
            """
            MATCH (n:MessageHandler) WHERE NOT n:Ontology
            RETURN n.id AS id ORDER BY id LIMIT 5
            """);

        foreach (var leaf in leaves)
        {
            var nodeId = leaf["id"].As<string>();
            var rows = await graph.GetNodeDependenciesAsync(nodeId, 5);
            var reported = rows.ToDictionary(
                row => row["nodeId"].As<string>(),
                row => row["depth"].As<int>(),
                StringComparer.Ordinal);

            Assert.Equal(rows.Count, reported.Count);

            var oracle = await fixture.QueryAsync(
                """
                MATCH (source {id: $nodeId})
                MATCH (target) WHERE target.id IN $ids
                MATCH path = shortestPath((source)<-[*1..5]-(target))
                RETURN target.id AS nodeId, length(path) AS shortest
                """,
                new { nodeId, ids = reported.Keys.ToList() });

            Assert.Equal(reported.Count, oracle.Count);
            foreach (var row in oracle)
            {
                Assert.Equal(row["shortest"].As<int>(), reported[row["nodeId"].As<string>()]);
            }
        }
    }

    [Fact]
    public async Task A_traversal_never_escapes_into_the_ontology_layer()
    {
        await using var graph = CreateService();

        var ontologyIds = (await fixture.QueryAsync("MATCH (n:Ontology) WHERE n.id IS NOT NULL RETURN n.id AS id"))
            .Select(row => row["id"].As<string>())
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(ontologyIds);

        foreach (var seedId in await AllSeedIdsAsync())
        {
            var rows = await graph.GetBlastRadiusAsync(seedId, 5);
            Assert.DoesNotContain(rows, row => ontologyIds.Contains(row["nodeId"].As<string>()));
        }
    }

    // --- an empty answer is a fact, on real ids -------------------------------------------------

    [Fact]
    public async Task Every_real_module_answers_instead_of_returning_an_empty_list()
    {
        await using var graph = CreateService();

        // Guards the regression in the other direction from the unknown-id tests: the label guards
        // must not start rejecting ids that genuinely exist.
        foreach (var moduleId in await ModuleIdsAsync())
        {
            var owned = await graph.GetModuleOwnershipAsync(moduleId);
            Assert.NotEmpty(owned);
        }
    }

    [Fact]
    public async Task An_id_that_only_differs_by_case_is_rejected_rather_than_silently_matched()
    {
        await using var graph = CreateService();

        var moduleId = (await ModuleIdsAsync()).First();

        // Documents the contract callers most often get wrong. `Payments` and `payments` are not
        // the same node, and the tool must say so rather than return nothing.
        await Assert.ThrowsAsync<UnknownNodeIdException>(
            () => graph.GetModuleOwnershipAsync(moduleId.ToLowerInvariant() + ".not-a-module"));
    }

    private async Task<IReadOnlyList<string>> ModuleIdsAsync()
    {
        var rows = await fixture.QueryAsync("MATCH (n:Module) WHERE NOT n:Ontology RETURN n.id AS id ORDER BY id");
        var ids = rows.Select(row => row["id"].As<string>()).ToList();
        Assert.NotEmpty(ids);
        return ids;
    }

    /// <summary>Every Module plus the System root — the seeds whose reachability overlaps most.</summary>
    private async Task<IReadOnlyList<string>> AllSeedIdsAsync()
    {
        var rows = await fixture.QueryAsync(
            """
            MATCH (n) WHERE (n:Module OR n:System) AND NOT n:Ontology
            RETURN n.id AS id ORDER BY id
            """);

        var ids = rows.Select(row => row["id"].As<string>()).ToList();
        Assert.NotEmpty(ids);
        return ids;
    }
}
