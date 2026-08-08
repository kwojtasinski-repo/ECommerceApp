using KgMcp.Core;
using Testcontainers.Neo4j;

namespace KgMcp.Tests;

public sealed class Neo4jContainerTests : IAsyncLifetime
{
    private readonly Neo4jContainer _container = new Neo4jBuilder("neo4j:5.26.29-community").Build();

    public Task InitializeAsync()
    {
        return _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Graph_service_uses_a_read_transaction_against_ephemeral_neo4j()
    {
        await using var graph = new KgGraphService(_container.GetConnectionString());
        var rows = await graph.QueryAsync("RETURN 1 AS value");
        Assert.Equal(1L, rows.Single()["value"]);
    }
}
