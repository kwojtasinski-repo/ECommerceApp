using Neo4j.Driver;

namespace KgMcp.Core;

public sealed class KgGraphService : IAsyncDisposable
{
    private readonly IDriver _driver;

    public KgGraphService(string uri)
    {
        _driver = GraphDatabase.Driver(uri, AuthTokens.None);
    }

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(string cypher, object? parameters = null, CancellationToken cancellationToken = default)
    {
        if (parameters is IDictionary<string, object?> parameterMap)
        {
            var candidate = parameterMap.TryGetValue("nodeId", out var nodeId) ? nodeId : parameterMap.TryGetValue("id", out var id) ? id : null;
            if (candidate is string candidateId)
            {
                await EnsureUnambiguousIdAsync(candidateId, cancellationToken);
            }
        }

        await using var session = _driver.AsyncSession();
        return await session.ExecuteReadAsync(async transaction =>
        {
            var cursor = await transaction.RunAsync(cypher, parameters as IDictionary<string, object?>);
            var records = await cursor.ToListAsync();
            return records.Select(record => (IReadOnlyDictionary<string, object?>)record.Values.ToDictionary(pair => pair.Key, pair => pair.Value)).ToList();
        });
    }

    private async Task EnsureUnambiguousIdAsync(string nodeId, CancellationToken cancellationToken)
    {
        await using var session = _driver.AsyncSession();
        var matches = await session.ExecuteReadAsync(async transaction =>
        {
            var cursor = await transaction.RunAsync("MATCH (n {id: $nodeId}) RETURN labels(n)[0] AS label", new Dictionary<string, object?> { ["nodeId"] = nodeId });
            return await cursor.ToListAsync();
        });

        if (matches.Count > 1)
        {
            var labels = string.Join(", ", matches.Select(match => match["label"].As<string>()));
            throw new InvalidOperationException($"ambiguous id '{nodeId}' matched {matches.Count} nodes across labels [{labels}]");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _driver.DisposeAsync();
    }
}
