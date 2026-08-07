using System.Globalization;
using KgCodegen.Core.Model;

namespace KgCodegen.Core.Emit;

public static class CypherEmitter
{
    public static string Emit(Graph graph, IReadOnlyList<string> header)
    {
        var lines = new List<string>(header);
        foreach (var label in graph.Nodes.Select(x => x.Label).Distinct().Order())
            lines.Add($"CREATE CONSTRAINT {label.ToLowerInvariant()}_id IF NOT EXISTS FOR (n:{label}) REQUIRE n.id IS UNIQUE;");

        foreach (var node in graph.Nodes.OrderBy(x => x.Label).ThenBy(x => x.Id))
        {
            var properties = string.Join(", ", node.Properties.OrderBy(x => x.Key)
                .Select(x => $"{x.Key}: {Value(x.Value)}"));
            var suffix = properties.Length == 0 ? "" : $", {properties}";
            lines.Add($"MERGE (n:{node.Label} {{id: '{Escape(node.Id)}'{suffix}}});");
        }

        foreach (var edge in graph.Edges.OrderBy(x => x.Type).ThenBy(x => x.SourceId).ThenBy(x => x.TargetId))
            lines.Add($"MATCH (s:{edge.SourceLabel} {{id: '{Escape(edge.SourceId)}'}}), (t:{edge.TargetLabel} {{id: '{Escape(edge.TargetId)}'}}) MERGE (s)-[:{edge.Type}]->(t);");
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string Value(object? value) => value switch
    {
        null => "null",
        string text => $"'{Escape(text)}'",
        bool boolean => boolean ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)!,
        _ => $"'{Escape(value.ToString() ?? "")}'"
    };

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "\\r").Replace("\n", "\\n");
}