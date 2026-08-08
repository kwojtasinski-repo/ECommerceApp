namespace KgMcp.Core;

public sealed record GraphNode(string Id, string Label, IReadOnlyDictionary<string, object?> Properties);

public sealed record GraphEdge(string Type, string FromId, string ToId);

public sealed record GraphNodeResult(GraphNode Node, IReadOnlyList<GraphEdge> Edges);

public sealed record GraphPath(string FromId, string ToId, IReadOnlyList<string> EdgeTypes, int Depth);

public sealed record OrphanContract(string Id, string Label, string Confidence, string Reason);

public sealed record ToolResult(string Tool, object? Data);
