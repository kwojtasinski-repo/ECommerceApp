namespace KgCodegen.Core.Model;

public sealed record CypherNode(string Label, string Id, IReadOnlyDictionary<string, object?> Properties);