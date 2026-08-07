namespace KgCodegen.Core.Model;

public sealed record CypherEdge(string Type, string SourceLabel, string SourceId, string TargetLabel, string TargetId);