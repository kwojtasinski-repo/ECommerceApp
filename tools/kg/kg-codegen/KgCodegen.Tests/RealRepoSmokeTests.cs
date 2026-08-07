using KgCodegen.Core.Parsing;
using KgCodegen.Core.Spine;
using KgCodegen.Core.Validation;
using KgCodegen.Core.Ontology;

namespace KgCodegen.Tests;

public sealed class RealRepoSmokeTests
{
    [Fact]
    public void Real_repository_produces_all_phase_one_node_types_without_ontology_errors()
    {
        var root = FindRepositoryRoot();
        var resolver = new ModuleResolver();
        var graph = SpineCatalog.Create();
        var symbols = DomainSymbolIndex.Build(Path.Combine(root, "ECommerceApp.Domain"));
        var entity = new EntityParser(resolver).Parse(Path.Combine(root, "ECommerceApp.Infrastructure"), symbols);
        entity.Graph.MergeInto(graph);
        var repository = new RepositoryParser(resolver).Parse(Path.Combine(root, "ECommerceApp.Domain"), graph.Nodes.Where(x => x.Label == "Entity").ToList());
        repository.Graph.MergeInto(graph);
        var action = new ActionParser(resolver).Parse(Path.Combine(root, "ECommerceApp.Application"));
        action.Graph.MergeInto(graph);

        var report = GraphValidator.Validate(graph, OntologyLoader.Load(Path.Combine(root, "tools", "kg", "seed", "ontology.json")));
        Assert.Empty(report.Errors);
        Assert.All(new[] { "Module", "Entity", "Repository", "Action" }, label =>
            Assert.Contains(graph.Nodes, node => node.Label == label));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ECommerceApp.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate ECommerceApp repository root.");
    }
}