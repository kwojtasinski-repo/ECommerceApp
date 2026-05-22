using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using RagTools.Core;
using Testcontainers.Qdrant;
using Xunit;

namespace RagTools.Tests.E2E;

// ═════════════════════════════════════════════════════════════════════════════
// LEVEL B — Chunker → Qdrant payload assertions (always run, no ONNX model)
//
// These tests:
//   1. Start Qdrant in a Docker container via Testcontainers.
//   2. Chunk the fixture documents using MarkdownChunker (whitespace counter).
//   3. Upsert points directly using QdrantClient with stub random vectors.
//   4. Scroll and verify the stored payload (text, breadcrumb, rel_path).
//
// No ONNX model is needed — embeddings are random stubs.
// Requires: Docker Desktop running (Testcontainers starts Qdrant automatically).
// ═════════════════════════════════════════════════════════════════════════════

[Trait("Category", "E2E-AutoMode-LevelB")]
public sealed class ChunkerAutoMode_LevelB : IAsyncLifetime
{
    private QdrantContainer? _container;
    private QdrantClient? _client;
    private string _collection = string.Empty;
    private static readonly ITokenCounter Counter = BertTokenCounter.FromModelDir("/nonexistent/path");
    private const int VectorDim = 4; // small stub vectors

    // ── IAsyncLifetime ────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        _container = new QdrantBuilder("qdrant/qdrant:v1.13.6")
            .WithPortBinding(6334, assignRandomHostPort: true)
            .Build();
        await _container.StartAsync();

        var grpcPort = _container.GetMappedPublicPort(6334);
        _client = new QdrantClient(_container.Hostname, grpcPort);
        _collection = $"levelb_{Guid.NewGuid():N}"[..20];

        await _client.CreateCollectionAsync(_collection, new VectorParams
        {
            Size = VectorDim,
            Distance = Distance.Cosine,
        });

        // Ingest both fixture docs.
        await UpsertDocAsync(AutoModeE2EFixture.H4Doc,           AutoModeE2EFixture.H4RelPath);
        await UpsertDocAsync(AutoModeE2EFixture.ShortSectionsDoc, AutoModeE2EFixture.ShortRelPath);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_container is not null)
            await _container.DisposeAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static MarkdownChunker AutoChunker() => new(new ChunkerSection
    {
        MaxTokens = 800, MinTokens = 40, OverlapTokens = 0,
        SplitOnHeadingsRaw = "auto",
    }, Counter);

    private async Task UpsertDocAsync(string markdown, string relPath)
    {
        var chunks = AutoChunker().Chunk(markdown, relPath);
        var rng = new Random(42);
        var points = chunks.Select((c, i) =>
        {
            // Random stub vector — only payload matters for Level B.
            var vec = Enumerable.Range(0, VectorDim).Select(_ => (float)rng.NextDouble()).ToArray();
            return new PointStruct
            {
                Id = (ulong)(Math.Abs(relPath.GetHashCode()) * 1000 + i + 1),
                Vectors = vec,
                Payload =
                {
                    ["rel_path"]  = relPath,
                    ["breadcrumb"] = c.Breadcrumb,
                    ["text"]      = c.Text,
                },
            };
        }).ToList();

        await _client!.UpsertAsync(_collection, points);
    }

    private async Task<List<(string Breadcrumb, string Text)>> ScrollAsync(string relPath)
    {
        var result = new List<(string, string)>();
        PointId? offset = null;

        do
        {
            var response = await _client!.ScrollAsync(
                _collection,
                limit: 100,
                offset: offset,
                payloadSelector: new WithPayloadSelector { Enable = true });

            foreach (var pt in response.Result)
            {
                var payload = pt.Payload;
                var path = payload.TryGetValue("rel_path", out var rp) ? rp.StringValue : "";
                if (!path.Equals(relPath, StringComparison.OrdinalIgnoreCase)) continue;
                var bc   = payload.TryGetValue("breadcrumb", out var b) ? b.StringValue : "";
                var text = payload.TryGetValue("text",       out var t) ? t.StringValue : "";
                result.Add((bc, text));
            }

            offset = response.NextPageOffset;
        }
        while (offset is not null);

        return result;
    }

    // ── Tests ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task LevelB_AutoModeProducesMoreChunksThanExplicit()
    {
        var autoChunks = await ScrollAsync(AutoModeE2EFixture.H4RelPath);
        var autoCount = autoChunks.Count;

        var explicitCount = new MarkdownChunker(new ChunkerSection
        {
            MaxTokens = 800, MinTokens = 40, OverlapTokens = 0,
            SplitOnHeadings = [1, 2, 3],
        }, Counter).Chunk(AutoModeE2EFixture.H4Doc, AutoModeE2EFixture.H4RelPath).Count;

        Assert.True(autoCount > explicitCount,
            $"Auto mode should store more points due to H4 splits: stored={autoCount}, explicit={explicitCount}");
    }

    [Fact]
    public async Task LevelB_H4ContentPresentInQdrantPayload()
    {
        var chunks = await ScrollAsync(AutoModeE2EFixture.H4RelPath);
        var allText = string.Join(" ", chunks.Select(c => c.Text));

        // Content from H4 "Versioning Strategy"
        Assert.Contains("Never break existing versions", allText);
        Assert.Contains("Deprecation requires a minimum six-month notice period", allText);
        // Content from H4 "Status Codes"
        Assert.Contains("Use 201 for successful POST", allText);
        Assert.Contains("Use 409 for conflicts", allText);
    }

    [Fact]
    public async Task LevelB_TrailingSmallH4NotLost_InQdrantPayload()
    {
        var chunks = await ScrollAsync(AutoModeE2EFixture.H4RelPath);
        var allText = string.Join(" ", chunks.Select(c => c.Text));

        Assert.Contains("Connection pooling is handled exclusively", allText);
    }

    [Fact]
    public async Task LevelB_ShortSectionPreservedViaForwardMerge_InQdrantPayload()
    {
        var chunks = await ScrollAsync(AutoModeE2EFixture.ShortRelPath);
        var allText = string.Join(" ", chunks.Select(c => c.Text));

        Assert.Contains("See MIGRATION.md", allText);
    }

    [Fact]
    public async Task LevelB_AllShortSectionsFixtureContentPresent()
    {
        var chunks = await ScrollAsync(AutoModeE2EFixture.ShortRelPath);
        var allText = string.Join(" ", chunks.Select(c => c.Text));

        Assert.Contains("Major rewrite of the order processing subsystem", allText);
        Assert.Contains("See MIGRATION.md", allText);
        Assert.Contains("The CreateOrder method now requires a UserId parameter", allText);
        Assert.Contains("LegacyOrderService", allText);
    }
}
