using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace RagTools.Tests.E2E;

[Trait("Category", "E2E")]
[Collection(RagTestCollection.Name)]
public sealed class HttpCrossCollectionConfigE2ETests : IClassFixture<HttpIngestE2EFixture>, IDisposable
{
    private readonly HttpIngestE2EFixture _fx;
    private readonly SharedOnnxFixture _sharedOnnx;

    public HttpCrossCollectionConfigE2ETests(
        HttpIngestE2EFixture fixture,
        SharedOnnxFixture sharedOnnx,
        ITestOutputHelper output)
    {
        _fx = fixture;
        _sharedOnnx = sharedOnnx;
        _fx.Sink.SetOutput(output);
        sharedOnnx.Sink.SetOutput(output);
    }

    public void Dispose()
    {
        _fx.Sink.SetOutput(null);
        _sharedOnnx.Sink.SetOutput(null);
    }

    [Fact]
    public async Task QueryDocs_IsolatesCollections_AndAppliesPerCollectionWeights()
    {
        Assert.True(_fx.IsAvailable, _fx.SkipReason);

        var collectionA = $"p38_a_{Guid.NewGuid():N}"[..14];
        var collectionB = $"p38_b_{Guid.NewGuid():N}"[..14];

        var zipA = BuildZip(
            ragConfigYaml: BuildRagConfigYaml(boostA: 9.0f, boostB: 0.1f, scoreThreshold: 0.0f),
            docs: new Dictionary<string, string>
            {
                ["docs/a/shared.md"] = SharedBody,
                ["docs/b/shared.md"] = SharedBody,
                ["docs/private/a-only.md"] = "# A private\n\nalpha-only-marker-9f5bf0ea",
            });

        var zipB = BuildZip(
            ragConfigYaml: BuildRagConfigYaml(boostA: 0.1f, boostB: 9.0f, scoreThreshold: 0.9f),
            docs: new Dictionary<string, string>
            {
                ["docs/a/shared.md"] = SharedBody,
                ["docs/b/shared.md"] = SharedBody,
                ["docs/private/b-only.md"] = "# B private\n\nbeta-only-marker-8b8bb31d",
            });

        await _fx.UploadZipAndWaitAsync(collectionA, zipA);
        await _fx.UploadZipAndWaitAsync(collectionB, zipB);

        var storedA = await _fx.Store!.FetchConfigAsync(collectionA);
        var storedB = await _fx.Store.FetchConfigAsync(collectionB);
        Assert.NotNull(storedA);
        Assert.NotNull(storedB);

        using var weightedA = await _fx.CallMcpToolAsync(
            collectionA,
            "query_docs",
            new { question = SharedQuery, top_k = 5 });

        using var weightedB = await _fx.CallMcpToolAsync(
            collectionB,
            "query_docs",
            new { question = SharedQuery, top_k = 5 });

        Assert.True(weightedA.RootElement.GetProperty("hits").GetArrayLength() > 0);
        Assert.True(weightedB.RootElement.GetProperty("hits").GetArrayLength() > 0);

        using var isolateA = await _fx.CallMcpToolAsync(
            collectionA,
            "query_docs",
            new { question = "alpha-only-marker-9f5bf0ea", top_k = 3 });

        using var isolateB = await _fx.CallMcpToolAsync(
            collectionB,
            "query_docs",
            new { question = "alpha-only-marker-9f5bf0ea", top_k = 3 });

        var isolateTopA = isolateA.RootElement.GetProperty("hits")[0].GetProperty("rel_path").GetString();
        Assert.Equal("docs/private/a-only.md", isolateTopA);

        var bHits = isolateB.RootElement.GetProperty("hits").EnumerateArray().ToList();
        Assert.DoesNotContain(bHits, hit =>
            string.Equals(hit.GetProperty("rel_path").GetString(), "docs/private/a-only.md", StringComparison.Ordinal));
    }

    private const string SharedQuery = "shared-weight-anchor-zeta";

    private const string SharedBody =
        "# Shared\n\nThis document contains shared-weight-anchor-zeta used for weight-isolation tests.";

        private static string BuildRagConfigYaml(float boostA, float boostB, float scoreThreshold)
    {
        return $"""
embedder:
  model: BAAI/bge-m3
ranking:
  weights:
        - pattern: "docs/a/**"
      weight: {boostA:0.0}
        - pattern: "docs/b/**"
      weight: {boostB:0.0}
query:
  fetch_k: 20
    score_threshold: {scoreThreshold:0.0}
""";
    }

    private static byte[] BuildZip(string ragConfigYaml, IReadOnlyDictionary<string, string> docs)
    {
        const string metadataRulesYaml = """
doc_kind_rules:
  - glob: "docs/**/*.md"
    kind: doc
""";

        const string queriesYaml = """
named_queries:
  - name: default
    question: shared-weight-anchor-zeta
    top_k: 5
""";

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "rag-config.yaml", ragConfigYaml);
            WriteEntry(zip, "metadata-rules.yaml", metadataRulesYaml);
            WriteEntry(zip, "queries.yaml", queriesYaml);

            foreach (var doc in docs)
            {
                WriteEntry(zip, doc.Key, doc.Value);
            }
        }

        return ms.ToArray();
    }

    private static void WriteEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }
}
