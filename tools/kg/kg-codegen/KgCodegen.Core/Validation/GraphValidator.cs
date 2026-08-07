using KgCodegen.Core.Model;
using KgCodegen.Core.Ontology;

namespace KgCodegen.Core.Validation;

public static class GraphValidator
{
    public static ValidationReport Validate(Graph graph, OntologyIndex ontology)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var byKey = graph.Nodes.ToDictionary(x => (x.Label, x.Id));

        foreach (var node in graph.Nodes)
            if (!ontology.KnownLabels.Contains(node.Label))
                warnings.Add($"Unknown node label '{node.Label}' for '{node.Id}'.");

        foreach (var edge in graph.Edges)
        {
            if (!byKey.ContainsKey((edge.SourceLabel, edge.SourceId)))
                errors.Add($"Missing source node '{edge.SourceLabel}:{edge.SourceId}'.");
            if (!byKey.ContainsKey((edge.TargetLabel, edge.TargetId)))
                errors.Add($"Missing target node '{edge.TargetLabel}:{edge.TargetId}'.");
            if (byKey.TryGetValue((edge.SourceLabel, edge.SourceId), out var source) && source.Label != edge.SourceLabel)
                errors.Add($"Source label mismatch for '{edge.SourceId}'.");
            if (byKey.TryGetValue((edge.TargetLabel, edge.TargetId), out var target) && target.Label != edge.TargetLabel)
                errors.Add($"Target label mismatch for '{edge.TargetId}'.");
            if (!ontology.AllowedEdges.Contains($"{edge.SourceLabel}|{edge.TargetLabel}|{edge.Type}"))
                errors.Add($"Undeclared edge '{edge.SourceLabel}-[:{edge.Type}]->{edge.TargetLabel}'.");
        }

        return new ValidationReport(errors, warnings);
    }
}