using System.Text.Json;
using System.Text.Json.Serialization;

namespace RagTools.Mcp.Tools;

/// <summary>
/// Shared JSON options for MCP tool responses. Keys are already snake_case
/// (written explicitly in anonymous types), so no naming policy is applied —
/// the only deviation from defaults is omitting null-valued properties so
/// the wire format matches the Python server contract.
///
/// Failure envelopes follow the same shape used by HTTP controllers
/// (see <see cref="RagTools.Mcp.Query.QueryOutcomeExtensions"/>):
///   <code>{ "error": "&lt;message&gt;", "code": "&lt;EnumName&gt;", "details"?: { … } }</code>
/// </summary>
internal static class McpJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
