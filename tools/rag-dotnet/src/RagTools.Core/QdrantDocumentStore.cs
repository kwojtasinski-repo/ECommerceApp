using Qdrant.Client;
using Qdrant.Client.Grpc;
using System.Text.Json;

namespace RagTools.Core;

/// <summary>
/// Qdrant-backed implementation of <see cref="IDocumentStore"/>.
///
/// Step 0: delegates chunk upsert / delete / search to the existing QdrantStore.
/// Step 2: StoreDocumentAsync / FetchContentAsync — full file content stored as a
///         zero-vector Qdrant point (doc_kind = "full_content").
///
/// Registered in DI as:
///   services.AddSingleton&lt;IDocumentStore, QdrantDocumentStore&gt;();
/// </summary>
public sealed class QdrantDocumentStore : IDocumentStore
{
    // Each collection gets its own QdrantStore wrapper (connection is per-collection).
    // For Step 0 the server is single-collection so only one store is needed.
    private readonly QdrantClient _client;
    private readonly Dictionary<string, QdrantStore> _stores = [];
    private readonly string _qdrantUrl;
    // Cached vector dimension per collection (read from Qdrant collection info on first use).
    private readonly Dictionary<string, int> _dimensions = [];

    public QdrantDocumentStore(string qdrantUrl)
    {
        _qdrantUrl = qdrantUrl;
        var uri = new Uri(qdrantUrl);
        var grpcPort = uri.Port == 6333 ? 6334 : uri.Port;
        _client = new QdrantClient(uri.Host, grpcPort);
    }

    // ── IDocumentStore.EnsureCollectionAsync / RecreateCollectionAsync ────────

    public async Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        var store = GetStore(collection);
        await store.EnsureCollectionAsync(dimensions, ct);
    }

    public async Task RecreateCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        var store = GetStore(collection);
        await store.RecreateCollectionAsync(dimensions, ct);
    }

    // ── Ingest path ───────────────────────────────────────────────────────────

    public async Task UpsertChunksAsync(string collection, IReadOnlyList<RagPoint> chunks, CancellationToken ct = default)
    {
        var store = GetStore(collection);
        await store.UpsertAsync(chunks, ct);
    }

    public Task DeleteByPathsAsync(string collection, IEnumerable<string> relPaths, CancellationToken ct = default)
    {
        var store = GetStore(collection);
        return store.DeleteByPathsAsync(relPaths, ct);
    }

    /// <summary>
    /// Store config (and embedded glossary) as a structured JSON point per collection.
    /// doc_kind = "__config__", point ID = DeterministicId.ForConfig(collection, DocKind.Config).
    /// YAML was parsed before calling this — only clean JSON reaches Qdrant.
    /// </summary>
    public async Task StoreConfigAsync(string collection, RagConfigPayload config, CancellationToken ct = default)
    {
        var dims = await GetDimensionsAsync(collection, ct);
        var id   = DeterministicId.ForConfig(collection, DocKind.Config);
        var json = JsonSerializer.Serialize(config);

        var point = new PointStruct
        {
            Id      = new PointId { Uuid = id.ToString() },
            Vectors = new float[dims],
            Payload =
            {
                ["doc_kind"]       = DocKind.Config,
                ["schema_version"] = config.SchemaVersion,
                ["payload_json"]   = json,     // full config serialized as single JSON string
                ["ingested_at"]    = DateTimeOffset.UtcNow.ToString("O"),
            },
        };

        await _client.UpsertAsync(collection, [point], cancellationToken: ct);
    }

    /// <summary>Fetch the stored config for a collection. Returns null if not yet uploaded.</summary>
    public async Task<RagConfigPayload?> FetchConfigAsync(string collection, CancellationToken ct = default)
    {
        var id = DeterministicId.ForConfig(collection, DocKind.Config);
        var points = await _client.RetrieveAsync(
            collection,
            [new PointId { Uuid = id.ToString() }],
            withPayload: true,
            cancellationToken: ct);

        if (points.Count == 0) return null;

        if (!points[0].Payload.TryGetValue("payload_json", out var json)) return null;

        try { return JsonSerializer.Deserialize<RagConfigPayload>(json.StringValue); }
        catch { return null; }
    }

    /// <summary>
    /// Upsert a full-content point (doc_kind = full_content, zero vector).
    /// Point ID = DeterministicId.ForContent(collection, doc.RelPath).
    /// The zero vector has the same dimension as the chunk vectors — read from Qdrant
    /// collection info on first call (cached per collection).
    /// </summary>
    public async Task StoreDocumentAsync(string collection, ContentDocument doc, CancellationToken ct = default)
    {
        var dims = await GetDimensionsAsync(collection, ct);
        var zeroVec = new float[dims];
        var id = DeterministicId.ForContent(collection, doc.RelPath);

        // Serialize the ContentDocument payload as structured JSON fields.
        var payload = new Dictionary<string, Value>
        {
            ["doc_kind"]    = DocKind.FullContent,
            ["rel_path"]    = doc.RelPath,
            ["doc_type"]    = doc.DocKind,
            ["bc"]          = doc.Bc ?? string.Empty,
            ["title"]       = doc.Title ?? string.Empty,
            ["content"]     = doc.Content,
            ["ingested_at"] = doc.IngestedAt.ToString("O"),
        };

        if (doc.Metadata is { Count: > 0 })
            payload["metadata"] = JsonSerializer.Serialize(doc.Metadata);

        var point = new PointStruct
        {
            Id   = new PointId { Uuid = id.ToString() },
            Vectors = zeroVec,
            Payload = { payload },
        };

        await _client.UpsertAsync(collection, [point], cancellationToken: ct);
    }

    // ── Query path ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(
        string collection, float[] queryVector, SearchOptions opts, CancellationToken ct = default)
    {
        var store = GetStore(collection);
        var hits = await store.SearchAsync(
            queryVector,
            opts.TopK,
            opts.ScoreThreshold,
            docKindFilter: opts.DocKindFilter,
            adrIdFilter: opts.AdrIdFilter,
            historyFieldFilter: opts.HistoryFieldFilter,
            cancellationToken: ct);

        return hits.Select(h => new DocumentSearchResult(
            Score: h.Score,
            RelPath: h.RelPath,
            DocTitle: h.DocTitle,
            DocKind: h.DocKind,
            AdrId: h.AdrId,
            Breadcrumb: h.Breadcrumb,
            StartLine: h.StartLine,
            Text: h.Text)).ToList();
    }

    /// <summary>
    /// Fetch full document content by relPath using the deterministic content_id.
    /// Returns null if no content point exists (caller falls back to disk read).
    /// </summary>
    public async Task<ContentDocument?> FetchContentAsync(string collection, string relPath, CancellationToken ct = default)
    {
        var id = DeterministicId.ForContent(collection, relPath);
        var points = await _client.RetrieveAsync(
            collection,
            [new PointId { Uuid = id.ToString() }],
            withPayload: true,
            cancellationToken: ct);

        if (points.Count == 0) return null;

        var p = points[0].Payload;
        if (!p.TryGetValue("content", out var content)) return null;

        IDictionary<string, string>? meta = null;
        if (p.TryGetValue("metadata", out var metaJson) && !string.IsNullOrEmpty(metaJson.StringValue))
        {
            try { meta = JsonSerializer.Deserialize<Dictionary<string, string>>(metaJson.StringValue); }
            catch { /* ignore malformed metadata */ }
        }

        return new ContentDocument(
            RelPath:    p.TryGetValue("rel_path",    out var rp) ? rp.StringValue    : relPath,
            DocKind:    p.TryGetValue("doc_type",    out var dk) ? dk.StringValue    : DocKind.FullContent,
            Bc:         p.TryGetValue("bc",          out var bc) ? bc.StringValue    : null,
            Title:      p.TryGetValue("title",       out var ti) ? ti.StringValue    : null,
            Content:    content.StringValue,
            IngestedAt: p.TryGetValue("ingested_at", out var ia)
                        && DateTimeOffset.TryParse(ia.StringValue, out var dt) ? dt : DateTimeOffset.UtcNow,
            Metadata:   meta);
    }

    /// <summary>
    /// List all ADRs indexed in the collection.
    /// Stub for Step 0 — returns empty (list_adrs reads from disk via RagConfig.Workspace).
    /// Implemented in Step 3.
    /// </summary>
    public Task<IReadOnlyList<AdrSummary>> ListAdrsAsync(string collection, CancellationToken ct = default)
    {
        // Step 3 will query Qdrant for full_content points where doc_type = "adr".
        return Task.FromResult<IReadOnlyList<AdrSummary>>(Array.Empty<AdrSummary>());
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private QdrantStore GetStore(string collection)
    {
        if (_stores.TryGetValue(collection, out var existing))
            return existing;
        var store = QdrantStore.Connect(_qdrantUrl, collection);
        _stores[collection] = store;
        return store;
    }

    /// <summary>
    /// Read the vector dimension from Qdrant collection info (cached after first call).
    /// Falls back to 384 (MiniLM default) if the collection does not exist yet.
    /// </summary>
    private async Task<int> GetDimensionsAsync(string collection, CancellationToken ct)
    {
        if (_dimensions.TryGetValue(collection, out var cached))
            return cached;

        try
        {
            var info = await _client.GetCollectionInfoAsync(collection, ct);
            var dim = (int)(info.Config.Params.VectorsConfig.Params.Size);
            _dimensions[collection] = dim;
            return dim;
        }
        catch
        {
            // Collection may not exist yet — caller should call EnsureCollectionAsync first.
            const int defaultDim = 384;
            _dimensions[collection] = defaultDim;
            return defaultDim;
        }
    }

    public void Dispose()
    {
        foreach (var store in _stores.Values)
            store.Dispose();
        _client.Dispose();
    }
}
