namespace KgMcp.Core;

/// <summary>A row of an orphan-contract report. Confidence is deliberately part of the shape:
/// a flat list of ids would let a caller present a known false positive as dead code.</summary>
public sealed record OrphanContract(string Id, string Label, string Confidence, string Reason);

/// <summary>Envelope every tool returns, so a caller can tell which tool produced the payload.</summary>
public sealed record ToolResult(string Tool, object? Data);

/// <summary>Envelope for a failed tool call. Kept distinct from <see cref="ToolResult"/> so a
/// caller never mistakes an error for an empty result set.</summary>
public sealed record ToolError(string Tool, string Error, string? Remedy = null);
