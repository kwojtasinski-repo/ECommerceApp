using System.Text.Json;
using RagTools.Core;
using RagTools.Core.Adrs;
using RagTools.Core.History;
using RagTools.Core.Query;
using RagTools.Core.ReadDocs;
using RagTools.Mcp.Tools;

namespace RagTools.Tests.Tools;

/// <summary>
/// Wire-shape pinning tests. Any drift in JSON property names or envelope
/// structure trips a unit test here before it can break the Python parity
/// or the MCP protocol contract.
/// </summary>
public class RagToolsProjectorTests
{
    private static JsonElement SerializeAndParse(object projected)
    {
        var json = McpJson.Serialize(projected);
        return JsonDocument.Parse(json).RootElement;
    }

    // ── query_docs ───────────────────────────────────────────────────────────

    [Fact]
    public void Query_Success_PinsHitKeys()
    {
        var outcome = new QueryOutcome.Success(new QueryResponse("c", "q", new[]
        {
            new QueryHit(1, 0.91, "adr_main", "docs/adr/0001.md", "ADR > 0001", 10, "body"),
        }, TotalCandidates: 1));
        var root = SerializeAndParse(RagToolsProjector.ProjectQuery(outcome));

        var hit = root.GetProperty("hits")[0];
        Assert.Equal(1, hit.GetProperty("rank").GetInt32());
        Assert.Equal(0.91, hit.GetProperty("score").GetDouble());
        Assert.Equal("adr_main", hit.GetProperty("doc_kind").GetString());
        Assert.Equal("docs/adr/0001.md", hit.GetProperty("rel_path").GetString());
        Assert.Equal("ADR > 0001", hit.GetProperty("breadcrumb").GetString());
        Assert.Equal(10, hit.GetProperty("start_line").GetInt32());
        Assert.Equal("body", hit.GetProperty("text").GetString());
    }

    [Fact]
    public void Query_EmptySuccess_IncludesMessage()
    {
        var outcome = new QueryOutcome.Success(new QueryResponse("c", "q", Array.Empty<QueryHit>(), TotalCandidates: 0));
        var root = SerializeAndParse(RagToolsProjector.ProjectQuery(outcome));
        Assert.Equal(0, root.GetProperty("hits").GetArrayLength());
        Assert.True(root.TryGetProperty("message", out _));
    }

    // ── read_docs ────────────────────────────────────────────────────────────

    [Fact]
    public void ReadDocs_ChunksMode_PinsFileAndChunkKeys()
    {
        var file = new ReadDocsFile("docs/a.md", 0.85, "doc", ReadDocsMode.Chunks, Content: null, new[]
        {
            new ReadDocsChunk(1, 0.85, 5, "chunk-text"),
        });
        var outcome = new ReadDocsOutcome.Success(new ReadDocsResponse("c", "q", ReadDocsMode.Chunks, new[] { file }));
        var root = SerializeAndParse(RagToolsProjector.ProjectReadDocs(outcome));

        var f = root.GetProperty("files")[0];
        Assert.Equal("docs/a.md", f.GetProperty("rel_path").GetString());
        Assert.Equal(0.85, f.GetProperty("score").GetDouble());
        Assert.Equal("doc", f.GetProperty("doc_kind").GetString());
        Assert.Equal("chunks", f.GetProperty("mode").GetString());
        Assert.False(f.TryGetProperty("content", out _));

        var chunk = f.GetProperty("chunks")[0];
        Assert.Equal(1, chunk.GetProperty("rank").GetInt32());
        Assert.Equal(0.85, chunk.GetProperty("score").GetDouble());
        Assert.Equal(5, chunk.GetProperty("start_line").GetInt32());
        Assert.Equal("chunk-text", chunk.GetProperty("text").GetString());
    }

    [Fact]
    public void ReadDocs_FullMode_PinsContentKey_NoChunksKey()
    {
        var file = new ReadDocsFile("docs/a.md", 0.9, "doc", ReadDocsMode.Full, Content: "BODY", Array.Empty<ReadDocsChunk>());
        var outcome = new ReadDocsOutcome.Success(new ReadDocsResponse("c", "q", ReadDocsMode.Full, new[] { file }));
        var root = SerializeAndParse(RagToolsProjector.ProjectReadDocs(outcome));

        var f = root.GetProperty("files")[0];
        Assert.Equal("full", f.GetProperty("mode").GetString());
        Assert.Equal("BODY", f.GetProperty("content").GetString());
        Assert.False(f.TryGetProperty("chunks", out _));
    }

    [Fact]
    public void ReadDocs_EmptySuccess_IncludesMessage()
    {
        var outcome = new ReadDocsOutcome.Success(new ReadDocsResponse("c", "q", ReadDocsMode.Chunks, Array.Empty<ReadDocsFile>()));
        var root = SerializeAndParse(RagToolsProjector.ProjectReadDocs(outcome));
        Assert.Equal(0, root.GetProperty("files").GetArrayLength());
        Assert.True(root.TryGetProperty("message", out _));
    }

    // ── get_history ──────────────────────────────────────────────────────────

    [Fact]
    public void History_Success_PinsTopLevelAndChunkKeys()
    {
        var outcome = new HistoryOutcome.Success(new HistoryResponse("0016", "adr_id", new[]
        {
            new HistoryChunk("docs/adr/0016.md", "ADR > 0016", "adr_main", 1, "intro"),
        }));
        var root = SerializeAndParse(RagToolsProjector.ProjectHistory(outcome));

        Assert.Equal("0016", root.GetProperty("id").GetString());
        Assert.Equal("adr_id", root.GetProperty("history_field").GetString());
        Assert.Equal(1, root.GetProperty("chunk_count").GetInt32());

        var c = root.GetProperty("chunks")[0];
        Assert.Equal("docs/adr/0016.md", c.GetProperty("rel_path").GetString());
        Assert.Equal("ADR > 0016", c.GetProperty("breadcrumb").GetString());
        Assert.Equal("adr_main", c.GetProperty("doc_kind").GetString());
        Assert.Equal(1, c.GetProperty("start_line").GetInt32());
        Assert.Equal("intro", c.GetProperty("text").GetString());
    }

    [Fact]
    public void History_EmptySuccess_IncludesMessage_AndZeroCount()
    {
        var outcome = new HistoryOutcome.Success(new HistoryResponse("0099", "adr_id", Array.Empty<HistoryChunk>()));
        var root = SerializeAndParse(RagToolsProjector.ProjectHistory(outcome));
        Assert.Equal(0, root.GetProperty("chunk_count").GetInt32());
        Assert.Equal(0, root.GetProperty("chunks").GetArrayLength());
        Assert.True(root.TryGetProperty("message", out _));
    }

    // ── list_adrs ────────────────────────────────────────────────────────────

    [Fact]
    public void List_Success_PinsAdrKeys()
    {
        var outcome = new AdrListOutcome.Success(new AdrListResponse(new[]
        {
            new AdrSummary("0016", "Coupons", "docs/adr/0016/0016-coupons.md", 2, 1),
        }));
        var root = SerializeAndParse(RagToolsProjector.ProjectList(outcome));

        var adr = root.GetProperty("adrs")[0];
        Assert.Equal("0016", adr.GetProperty("id").GetString());
        Assert.Equal("Coupons", adr.GetProperty("title").GetString());
        Assert.Equal("docs/adr/0016/0016-coupons.md", adr.GetProperty("main_file").GetString());
        Assert.Equal(2, adr.GetProperty("amendments").GetInt32());
        Assert.Equal(1, adr.GetProperty("examples").GetInt32());

        // Python parity: top-level count
        Assert.Equal(1, root.GetProperty("count").GetInt32());

        // Pin: Python-legacy keys must NOT appear
        Assert.False(adr.TryGetProperty("adr_id", out _));
        Assert.False(adr.TryGetProperty("amendment_count", out _));
        Assert.False(adr.TryGetProperty("example_count", out _));
    }

    // ── failure envelope ────────────────────────────────────────────────────

    [Fact]
    public void Failure_WithoutDetails_OmitsDetailsKey()
    {
        var outcome = new QueryOutcome.Failure(QueryError.EmptyQuestion, "Question must not be empty.");
        var root = SerializeAndParse(RagToolsProjector.ProjectQuery(outcome));
        Assert.Equal("Question must not be empty.", root.GetProperty("error").GetString());
        Assert.Equal("EmptyQuestion", root.GetProperty("code").GetString());
        Assert.False(root.TryGetProperty("details", out _));
    }

    [Fact]
    public void Failure_WithDetails_IncludesDetailsObject()
    {
        var outcome = new QueryOutcome.Failure(
            QueryError.TopKOutOfRange,
            "out of range",
            new Dictionary<string, object?> { ["topK"] = 100, ["max"] = 20 });
        var root = SerializeAndParse(RagToolsProjector.ProjectQuery(outcome));
        var details = root.GetProperty("details");
        Assert.Equal(100, details.GetProperty("topK").GetInt32());
        Assert.Equal(20, details.GetProperty("max").GetInt32());
    }

    [Fact]
    public void Failure_WithEmptyDetails_OmitsDetailsKey()
    {
        var outcome = new QueryOutcome.Failure(
            QueryError.EmptyQuestion, "x", new Dictionary<string, object?>());
        var root = SerializeAndParse(RagToolsProjector.ProjectQuery(outcome));
        Assert.False(root.TryGetProperty("details", out _));
    }
}
