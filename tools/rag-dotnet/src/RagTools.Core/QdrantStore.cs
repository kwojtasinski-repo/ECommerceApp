using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace RagTools.Core;

/// <summary>
/// Qdrant vector store wrapper. Handles collection setup and point upsert/search.
/// Connects via gRPC to a Qdrant HTTP server (required for .NET — no embedded mode).
/// </summary>
public sealed class QdrantStore : IDisposable
{
    private readonly QdrantClient _client;
    private readonly string _collection;

    private QdrantStore(QdrantClient client, string collection)
    {
        _client = client;
        _collection = collection;
    }

    public static QdrantStore Connect(string url, string collection)
    {
        var uri = new Uri(url);
        // QdrantClient accepts host + port. Default gRPC port is 6334.
        var grpcPort = uri.Port == 6333 ? 6334 : uri.Port;
        var client = new QdrantClient(uri.Host, grpcPort);
        return new QdrantStore(client, collection);
    }

    /// <summary>Ensure the collection exists with the correct vector size.</summary>
    public async Task EnsureCollectionAsync(int dimensions, CancellationToken ct = default)
    {
        var exists = await _client.CollectionExistsAsync(_collection, ct);
        if (exists) return;

        await _client.CreateCollectionAsync(_collection,
            new VectorParams
            {
                Size = (ulong)dimensions,
                Distance = Distance.Cosine,
            },
            cancellationToken: ct);
    }

    /// <summary>Upsert a batch of points. Idempotent — same UUID is overwritten.</summary>
    public async Task UpsertAsync(IReadOnlyList<RagPoint> points, CancellationToken ct = default)
    {
        if (points.Count == 0) return;

        var qdrantPoints = points.Select(p =>
        {
            var payload = new Dictionary<string, Value>
            {
                ["rel_path"] = p.Payload.RelPath,
                ["doc_title"] = p.Payload.DocTitle,
                ["doc_kind"] = p.Payload.DocKind,
                ["breadcrumb"] = p.Payload.Breadcrumb,
                ["heading_path"] = p.Payload.HeadingPath,
                ["start_line"] = p.Payload.StartLine,
                ["end_line"] = p.Payload.EndLine,
                ["token_count"] = p.Payload.TokenCount,
                ["weight"] = p.Payload.Weight,
                ["text"] = p.Payload.Text,
            };
            if (p.Payload.AdrId is not null)
                payload["adr_id"] = p.Payload.AdrId;

            return new PointStruct
            {
                Id = p.Id,
                Vectors = p.Vector,
                Payload = { payload },
            };
        }).ToList();

        await _client.UpsertAsync(_collection, qdrantPoints, cancellationToken: ct);
    }

    /// <summary>Delete all points whose rel_path matches any of the given paths.</summary>
    public async Task DeleteByPathsAsync(IEnumerable<string> relPaths, CancellationToken ct = default)
    {
        foreach (var relPath in relPaths)
        {
            await _client.DeleteAsync(_collection,
                new Filter
                {
                    Must =
                    {
                        new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = "rel_path",
                                Match = new Match { Text = relPath },
                            }
                        }
                    }
                },
                cancellationToken: ct);
        }
    }

    /// <summary>Semantic search. Returns scored hits ordered by descending score.</summary>
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        float[] queryVector,
        int topK,
        float scoreThreshold,
        string? docKindFilter = null,
        string? adrIdFilter = null,
        CancellationToken cancellationToken = default)
    {
        Filter? filter = null;
        var conditions = new List<Condition>();

        if (docKindFilter is not null)
            conditions.Add(FieldCondition("doc_kind", docKindFilter));
        if (adrIdFilter is not null)
            conditions.Add(FieldCondition("adr_id", adrIdFilter));

        if (conditions.Count > 0)
        {
            filter = new Filter();
            filter.Must.AddRange(conditions);
        }

        var results = await _client.SearchAsync(
            _collection,
            queryVector,
            limit: (ulong)topK,
            scoreThreshold: scoreThreshold,
            filter: filter,
            payloadSelector: new WithPayloadSelector { Enable = true },
            cancellationToken: cancellationToken);

        return results.Select(r => new SearchHit(
            Score: r.Score,
            RelPath: r.Payload.TryGetValue("rel_path", out var rp) ? rp.StringValue : "",
            DocTitle: r.Payload.TryGetValue("doc_title", out var dt) ? dt.StringValue : "",
            DocKind: r.Payload.TryGetValue("doc_kind", out var dk) ? dk.StringValue : "",
            AdrId: r.Payload.TryGetValue("adr_id", out var ai) ? ai.StringValue : null,
            Breadcrumb: r.Payload.TryGetValue("breadcrumb", out var bc) ? bc.StringValue : "",
            Text: r.Payload.TryGetValue("text", out var tx) ? tx.StringValue : ""
        )).ToList();
    }

    private static Condition FieldCondition(string key, string value) =>
        new()
        {
            Field = new FieldCondition
            {
                Key = key,
                Match = new Match { Text = value },
            }
        };

    public void Dispose() => _client.Dispose();
}

public sealed record RagPoint(
    Guid Id,
    float[] Vector,
    RagPayload Payload);

public sealed record RagPayload(
    string RelPath,
    string DocTitle,
    string DocKind,
    string? AdrId,
    string Breadcrumb,
    string HeadingPath,
    int StartLine,
    int EndLine,
    int TokenCount,
    float Weight,
    string Text);

public sealed record SearchHit(
    float Score,
    string RelPath,
    string DocTitle,
    string DocKind,
    string? AdrId,
    string Breadcrumb,
    string Text);
