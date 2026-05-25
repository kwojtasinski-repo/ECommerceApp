using System.Text.Json;
using RagTools.Mcp.Tools;

namespace RagTools.Tests.Tools;

/// <summary>
/// Pins the shared MCP wire format: keys are written verbatim (already snake_case in
/// the anonymous types), and null-valued properties are omitted so the .NET server
/// matches the Python contract.
/// </summary>
public class McpJsonTests
{
    [Fact]
    public void Serialize_OmitsNullValuedProperties()
    {
        var json = McpJson.Serialize(new { error = "boom", code = "X", details = (object?)null });

        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
        Assert.True(doc.RootElement.TryGetProperty("code", out _));
        Assert.False(doc.RootElement.TryGetProperty("details", out _));
    }

    [Fact]
    public void Serialize_KeepsExplicitSnakeCaseKeys_Verbatim()
    {
        var json = McpJson.Serialize(new { rel_path = "a.md", doc_kind = "doc", start_line = 1 });

        var doc = JsonDocument.Parse(json);
        Assert.Equal("a.md", doc.RootElement.GetProperty("rel_path").GetString());
        Assert.Equal("doc",  doc.RootElement.GetProperty("doc_kind").GetString());
        Assert.Equal(1,      doc.RootElement.GetProperty("start_line").GetInt32());
    }

    [Fact]
    public void Serialize_EmitsNonNullValues()
    {
        var json = McpJson.Serialize(new { error = "boom", details = new Dictionary<string, object?> { ["topK"] = 99 } });

        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("details", out var details));
        Assert.Equal(99, details.GetProperty("topK").GetInt32());
    }
}
