using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using RagTools.Core.Ingest;

namespace RagTools.Tests;

/// <summary>
/// Smoke tests for <see cref="ZipBatchParser"/> — only orchestration mechanics.
/// All rule-detail tests live in <see cref="BatchValidatorTests"/> and use plain strings
/// (no ZIP) for speed and isolation.
/// </summary>
public sealed class ZipBatchParserTests
{
    private const string RagConfigYaml = """
        embedder:
          model: BAAI/bge-m3
        """;

    private const string MetadataYaml = """
        doc_kind_rules:
          - kind: adr_main
            paths: ["docs/adr/**"]
        """;

    private const string QueriesYaml = """
        named_queries:
          - id: q1
            doc_kind: adr_main
            text: hello
        """;

    private static ZipBatchParser NewSut() =>
        new(new BatchValidator(NullLogger<BatchValidator>.Instance),
            NullLogger<ZipBatchParser>.Instance);

    private static Stream BuildZip(params (string Path, string Content)[] entries)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                var entry = zip.CreateEntry(path);
                using var s = entry.Open();
                using var w = new StreamWriter(s, Encoding.UTF8);
                w.Write(content);
            }
        }
        ms.Position = 0;
        return ms;
    }

    private static Stream MinimalValidZip(params (string Path, string Content)[] extra)
    {
        var baseFiles = new[]
        {
            ("rag-config.yaml", RagConfigYaml),
            ("metadata-rules.yaml", MetadataYaml),
            ("queries.yaml", QueriesYaml),
        };
        return BuildZip(baseFiles.Concat(extra).ToArray());
    }

    [Fact]
    public async Task EmptyStream_ReturnsEmptyBody()
    {
        var outcome = await NewSut().ParseAsync(new MemoryStream());

        var failure = Assert.IsType<ZipParseOutcome.Failure>(outcome);
        Assert.Equal(BatchIngestError.EmptyBody, failure.Error);
    }

    [Fact]
    public async Task InvalidZipBytes_ReturnsInvalidZipArchive()
    {
        var garbage = new MemoryStream(Encoding.UTF8.GetBytes("not a zip"));

        var outcome = await NewSut().ParseAsync(garbage);

        var failure = Assert.IsType<ZipParseOutcome.Failure>(outcome);
        Assert.Equal(BatchIngestError.InvalidZipArchive, failure.Error);
    }

    [Fact]
    public async Task HappyPath_ReturnsParsedBatch_WithDocuments_AndRules()
    {
        var zip = MinimalValidZip(("docs/adr/0001.md", "# ADR-0001\n\nbody"));

        var outcome = await NewSut().ParseAsync(zip);

        var success = Assert.IsType<ZipParseOutcome.Success>(outcome);
        Assert.Single(success.Batch.Documents);
        Assert.Equal("docs/adr/0001.md", success.Batch.Documents[0].RelPath);
        Assert.Contains("# ADR-0001", success.Batch.Documents[0].Content);
        Assert.NotNull(success.Batch.BatchRules);
    }

    [Fact]
    public async Task Cancellation_DuringContentRead_Propagates()
    {
        var zip = MinimalValidZip(("a.md", "# a"), ("b.md", "# b"), ("c.md", "# c"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => NewSut().ParseAsync(zip, cts.Token));
    }

    [Fact]
    public async Task ContentReadback_PreservesUtf8AndNewlines()
    {
        const string content = "# Tytuł\r\n\r\nZażółć gęślą jaźń.\n— em-dash.";
        var zip = MinimalValidZip(("doc.md", content));

        var outcome = await NewSut().ParseAsync(zip);

        var success = Assert.IsType<ZipParseOutcome.Success>(outcome);
        Assert.Equal(content, success.Batch.Documents[0].Content);
    }

    [Fact]
    public async Task TempFile_IsCleanedUpAfterParse()
    {
        var tempDir = Path.GetTempPath();
        var before = new HashSet<string>(Directory.GetFiles(tempDir, "ragzip-*.zip"));

        var zip = MinimalValidZip(("doc.md", "# x"));
        _ = await NewSut().ParseAsync(zip);

        // DeleteOnClose disposes the temp file synchronously with the FileStream dispose.
        // Compare snapshots rather than counts to avoid races with concurrent test workers.
        var after = new HashSet<string>(Directory.GetFiles(tempDir, "ragzip-*.zip"));
        var leaked = after.Except(before).ToArray();
        Assert.Empty(leaked);
    }

    [Fact]
    public async Task ValidationFailure_FromValidator_IsForwarded()
    {
        // Skip rag-config.yaml → validator must reject with MissingRagConfigYaml.
        var zip = BuildZip(
            ("metadata-rules.yaml", MetadataYaml),
            ("queries.yaml", QueriesYaml),
            ("doc.md", "# x"));

        var outcome = await NewSut().ParseAsync(zip);

        var failure = Assert.IsType<ZipParseOutcome.Failure>(outcome);
        Assert.Equal(BatchIngestError.MissingRagConfigYaml, failure.Error);
    }
}
