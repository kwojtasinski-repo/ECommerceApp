using KgCodegen.Core.Emit;
using KgCodegen.Core.Model;
using KgCodegen.Core.Ontology;
using KgCodegen.Core.Validation;

namespace KgCodegen.Tests;

public sealed class GraphAndEmitterTests
{
    [Fact]
    public void Validator_accepts_declared_edge()
    {
        var graph = new Graph();
        graph.Nodes.Add(new CypherNode("Module", "Catalog", new Dictionary<string, object?>()));
        graph.Nodes.Add(new CypherNode("Entity", "Product", new Dictionary<string, object?>()));
        graph.Edges.Add(new CypherEdge("CONTAINS", "Module", "Catalog", "Entity", "Product"));
        var report = GraphValidator.Validate(graph, new OntologyIndex(new HashSet<string> { "Module", "Entity" }, new HashSet<string> { "Module|Entity|CONTAINS" }));
        Assert.Empty(report.Errors);
    }

    [Fact]
    public void Validator_rejects_undeclared_edge_and_warns_unknown_label()
    {
        var graph = new Graph();
        graph.Nodes.Add(new CypherNode("Alien", "x", new Dictionary<string, object?>()));
        graph.Nodes.Add(new CypherNode("Entity", "e", new Dictionary<string, object?>()));
        graph.Edges.Add(new CypherEdge("USES", "Alien", "x", "Entity", "e"));
        var report = GraphValidator.Validate(graph, new OntologyIndex(new HashSet<string> { "Entity" }, new HashSet<string>()));
        Assert.NotEmpty(report.Errors);
        Assert.Single(report.Warnings);
    }

    [Fact]
    public void Emitter_is_deterministic_and_escapes_strings()
    {
        var graph = new Graph();
        graph.Nodes.Add(new CypherNode("Entity", "a'b", new Dictionary<string, object?> { ["text"] = "line1\nline2\\x" }));
        var first = CypherEmitter.Emit(graph, ["// header"]);
        var second = CypherEmitter.Emit(graph, ["// header"]);
        Assert.Equal(first, second);
        Assert.Contains("a\\'b", first);
        Assert.Contains("line1\\nline2\\\\x", first);
    }
}