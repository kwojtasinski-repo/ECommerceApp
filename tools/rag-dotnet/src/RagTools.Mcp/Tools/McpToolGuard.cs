using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace RagTools.Mcp.Tools;

/// <summary>
/// Wraps a single MCP tool invocation in the project's standard error envelope:
/// <list type="bullet">
///   <item><see cref="McpException"/> and <see cref="OperationCanceledException"/> pass through unchanged.</item>
///   <item>Any other exception is logged at error level and re-thrown as a
///   <see cref="McpException"/> built by <see cref="ToolErrorSanitizer"/> so
///   no stack trace / absolute path leaks to the caller.</item>
/// </list>
/// Centralises the per-tool try/catch boilerplate that previously lived inline
/// in every <c>[McpServerTool]</c> method on <see cref="RagTools"/>.
///
/// Note: the ModelContextProtocol 1.3.0 SDK does not expose a per-invocation
/// interception filter that fires for attribute-decorated tools (its call-tool
/// filter only wraps the fallback handler), so this guard is invoked
/// explicitly inside each tool method. That keeps the wrapping logic in one
/// place without forking the SDK.
/// </summary>
public static class McpToolGuard
{
    public static async Task<T> RunAsync<T>(
        ILogger logger,
        string toolName,
        Func<CancellationToken, Task<T>> body,
        CancellationToken cancellationToken)
    {
        try
        {
            return await body(cancellationToken);
        }
        catch (McpException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Tool} failed", toolName);
            throw ToolErrorSanitizer.ToMcpException(ex);
        }
    }
}
