using Neo4j.Driver;
using Testcontainers.Neo4j;

namespace KgMcp.Tests;

/// <summary>
/// One ephemeral Neo4j for the whole suite, seeded with a small hand-written graph.
///
/// The fixture is deliberately NOT the generated seed: it is built shape by shape so each test
/// can point at the exact node it is about, and so a change in the real repository cannot quietly
/// turn a passing assertion into a vacuous one.
/// </summary>
public sealed class Neo4jFixture : IAsyncLifetime
{
    private readonly Neo4jContainer _container = new Neo4jBuilder("neo4j:5.26.29-community").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Writes the fixture graph. Write clauses are legitimate here and nowhere in KgMcp.Core —
    /// <see cref="ContractTests"/> pins that asymmetry.
    /// </summary>
    private async Task SeedAsync()
    {
        await using var driver = GraphDatabase.Driver(ConnectionString, AuthTokens.None);
        await using var session = driver.AsyncSession();
        await session.ExecuteWriteAsync(async transaction =>
        {
            await transaction.RunAsync(
                """
                // --- modules and a cross-module Message contract ---
                CREATE (sales:Module {id: 'M.Sales'})
                CREATE (inventory:Module {id: 'M.Inventory'})
                CREATE (placeOrder:Action {id: 'A.PlaceOrder'})
                CREATE (scheduleAction:Action {id: 'A.Schedule'})
                CREATE (sales)-[:CONTAINS]->(placeOrder)
                CREATE (sales)-[:CONTAINS]->(scheduleAction)

                // --- exposure surfaces: an Action is exposed by OUTGOING EXPOSED_BY edges ---
                CREATE (endpoint:Endpoint {id: 'E.PostOrder'})
                CREATE (page:Page {id: 'P.Checkout'})
                CREATE (placeOrder)-[:EXPOSED_BY]->(endpoint)
                CREATE (placeOrder)-[:EXPOSED_BY]->(page)

                // --- messages, one per orphan class ---
                CREATE (dead:Message {id: 'Msg.Dead'})
                CREATE (unwired:Message {id: 'Msg.Unwired'})
                CREATE (healthy:Message {id: 'Msg.Healthy'})
                CREATE (healthyHandler:MessageHandler {id: 'MH.Healthy'})
                CREATE (inventory)-[:CONTAINS]->(healthyHandler)
                CREATE (placeOrder)-[:PUBLISHES]->(dead)
                CREATE (placeOrder)-[:PUBLISHES]->(healthy)
                CREATE (healthy)-[:HANDLED_BY]->(healthyHandler)

                // --- queries: one with no caller (the known false-positive shape), one with no handler ---
                CREATE (noCaller:Query {id: 'Q.NoCaller'})
                CREATE (noHandler:Query {id: 'Q.NoHandler'})
                CREATE (crossQuery:Query {id: 'Q.Cross'})
                CREATE (queryHandler:QueryHandler {id: 'QH.Inventory'})
                CREATE (inventory)-[:CONTAINS]->(queryHandler)
                CREATE (noCaller)-[:HANDLED_BY]->(queryHandler)
                CREATE (placeOrder)-[:USES]->(noHandler)
                CREATE (placeOrder)-[:USES]->(crossQuery)
                CREATE (crossQuery)-[:HANDLED_BY]->(queryHandler)

                // --- jobs: every one is CONTAINS-owned, which is what defeats a naive orphan filter ---
                CREATE (runtimeJob:Job {id: 'J.Runtime'})
                CREATE (deferredJob:Job {id: 'J.Deferred', triggerMode: 'Deferred'})
                CREATE (scheduledJob:Job {id: 'J.Scheduled', triggerMode: 'Deferred'})
                CREATE (sales)-[:CONTAINS]->(runtimeJob)
                CREATE (sales)-[:CONTAINS]->(deferredJob)
                CREATE (sales)-[:CONTAINS]->(scheduledJob)
                CREATE (scheduleAction)-[:SCHEDULES]->(scheduledJob)

                // --- a second action with a genuinely overlapping edge shape ---
                // A.Refund shares EXPOSED_BY and PUBLISHES with A.PlaceOrder, so structural
                // similarity has something true to find. A.Schedule, whose only edge is
                // SCHEDULES, is the negative control: zero overlap must mean zero results.
                CREATE (refund:Action {id: 'A.Refund'})
                CREATE (sales)-[:CONTAINS]->(refund)
                CREATE (refundPage:Page {id: 'P.Refund'})
                CREATE (refundMessage:Message {id: 'Msg.Refund'})
                CREATE (refund)-[:EXPOSED_BY]->(refundPage)
                CREATE (refund)-[:PUBLISHES]->(refundMessage)
                CREATE (refundMessage)-[:HANDLED_BY]->(healthyHandler)

                // --- governance ---
                // Mirrors the ontology exactly: GOVERNED_BY originates at the exposure surface
                // (Endpoint/Page), never at the Action. P.Governed is a governed surface with no
                // Action behind it, so the derived actionId is legitimately null.
                CREATE (admin:Role {id: 'R.Admin'})
                CREATE (endpoint)-[:GOVERNED_BY]->(admin)
                CREATE (page)-[:GOVERNED_BY]->(admin)
                CREATE (bareSurface:Page {id: 'P.Governed'})
                CREATE (bareSurface)-[:GOVERNED_BY]->(admin)

                // --- one id deliberately shared by two labels ---
                CREATE (:Action {id: 'Dup.Shared'})
                CREATE (:Page {id: 'Dup.Shared'})
                """);

            // A 7-hop chain, longer than the depth clamp, so the clamp is observable in results
            // rather than only greppable in source.
            await transaction.RunAsync(
                """
                CREATE (s1:ScriptModule {id: 'S1'})
                CREATE (s2:ScriptModule {id: 'S2'})
                CREATE (s3:ScriptModule {id: 'S3'})
                CREATE (s4:ScriptModule {id: 'S4'})
                CREATE (s5:ScriptModule {id: 'S5'})
                CREATE (s6:ScriptModule {id: 'S6'})
                CREATE (s7:ScriptModule {id: 'S7'})
                CREATE (s8:ScriptModule {id: 'S8'})
                CREATE (s1)-[:DEPENDS_ON]->(s2)-[:DEPENDS_ON]->(s3)-[:DEPENDS_ON]->(s4)
                CREATE (s4)-[:DEPENDS_ON]->(s5)-[:DEPENDS_ON]->(s6)-[:DEPENDS_ON]->(s7)
                CREATE (s7)-[:DEPENDS_ON]->(s8)
                """);
        });
    }

    /// <summary>Current node and relationship counts, used to prove the tools never write.</summary>
    public async Task<(long Nodes, long Edges)> GetCountsAsync()
    {
        await using var driver = GraphDatabase.Driver(ConnectionString, AuthTokens.None);
        await using var session = driver.AsyncSession();
        return await session.ExecuteReadAsync(async transaction =>
        {
            var nodeCursor = await transaction.RunAsync("MATCH (n) RETURN count(n) AS value");
            var nodes = (await nodeCursor.SingleAsync())["value"].As<long>();
            var edgeCursor = await transaction.RunAsync("MATCH ()-[r]->() RETURN count(r) AS value");
            var edges = (await edgeCursor.SingleAsync())["value"].As<long>();
            return (nodes, edges);
        });
    }
}

[CollectionDefinition(Name)]
public sealed class Neo4jCollection : ICollectionFixture<Neo4jFixture>
{
    public const string Name = "neo4j";
}
