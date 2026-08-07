namespace KgCodegen.Core.Model;

public sealed class Graph
{
    public List<CypherNode> Nodes { get; } = [];
    public List<CypherEdge> Edges { get; } = [];

    public static Graph Empty() => new();

    public void MergeInto(Graph target)
    {
        foreach (var node in Nodes)
        {
            var existing = target.Nodes.FirstOrDefault(x => x.Label == node.Label && x.Id == node.Id);
            if (existing is null)
            {
                target.Nodes.Add(node);
                continue;
            }

            var properties = new Dictionary<string, object?>(existing.Properties);
            foreach (var property in node.Properties)
                properties[property.Key] = property.Value;
            target.Nodes[target.Nodes.IndexOf(existing)] = existing with { Properties = properties };
        }

        foreach (var edge in Edges)
            if (!target.Edges.Contains(edge)) target.Edges.Add(edge);
    }
}