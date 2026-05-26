using System.Text.RegularExpressions;
using ModelContextProtocol;

namespace RagTools.Mcp.Tools;

/// <summary>
/// Sanitizes tool-invocation exceptions into <see cref="McpException"/> with a
/// safe message — no absolute paths, no stack traces, length-capped. The full
/// exception is logged server-side; only the cleaned message reaches the client.
/// </summary>
internal static class ToolErrorSanitizer
{
    private static readonly Regex PathRegex = new(
        @"['""]?(?:[A-Za-z]:)?[\\/](?:[^\s'""):]+[\\/])+([^\s'"":)]+)['""]?",
        RegexOptions.Compiled);

    private const int MaxMessageLength = 500;

    public static string Sanitize(Exception exc)
    {
        var msg = string.IsNullOrEmpty(exc.Message) ? exc.GetType().Name : exc.Message;
        msg = PathRegex.Replace(msg, "<path>/$1");
        return msg.Length > MaxMessageLength ? msg[..MaxMessageLength] : msg;
    }

    public static McpException ToMcpException(Exception exc)
        => new($"{exc.GetType().Name}: {Sanitize(exc)}");
}
