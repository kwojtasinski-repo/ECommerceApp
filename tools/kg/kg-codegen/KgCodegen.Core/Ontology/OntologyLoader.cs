using System.Text.Json;

namespace KgCodegen.Core.Ontology;

public static class OntologyLoader
{
    public static OntologyIndex Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var labels = document.RootElement.GetProperty("nodes").EnumerateArray()
            .Select(x => x.GetProperty("label").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var edges = document.RootElement.GetProperty("relationships").EnumerateArray()
            .Select(x => $"{x.GetProperty("source").GetString()}|{x.GetProperty("target").GetString()}|{x.GetProperty("type").GetString()}")
            .ToHashSet(StringComparer.Ordinal);
        return new OntologyIndex(labels, edges);
    }
}