using KgCodegen.Core.Model;
using KgCodegen.Core.Ontology;
using KgCodegen.Core.Parsing;
using KgCodegen.Core.Spine;
using KgCodegen.Core.Validation;

namespace KgCodegen.Tests;

public sealed class PinnedRealGraphTests
{
    [Fact]
    public void Module_count_is_exactly_fourteen()
    {
        var graph = BuildRealGraph();

        Assert.Equal(14, graph.Nodes.Count(node => node.Label == "Module"));
    }

    [Fact]
    public void Coupon_entity_has_correct_table_and_fqcn()
    {
        var graph = BuildRealGraph();
        var coupon = Assert.Single(graph.Nodes, node => node.Label == "Entity" && node.Id == "ECommerceApp.Domain.Sales.Coupons.Coupon");

        Assert.Equal("Coupons", coupon.Properties["table"]);
    }

    [Fact]
    public void Coupon_repository_edges_are_exact_not_prefix_matched()
    {
        var graph = BuildRealGraph();
        var couponEdges = graph.Edges
            .Where(edge => edge.Type == "PERSISTED_BY" && edge.SourceId == "ECommerceApp.Domain.Sales.Coupons.Coupon")
            .ToArray();

        Assert.Contains(couponEdges, edge => edge.TargetId == "ECommerceApp.Domain.Sales.Coupons.ICouponRepository");
        Assert.DoesNotContain(couponEdges, edge => edge.TargetId == "ECommerceApp.Domain.Sales.Coupons.ICouponUsedRepository");
        Assert.DoesNotContain(couponEdges, edge => edge.TargetId == "ECommerceApp.Domain.Sales.Coupons.ICouponApplicationRecordRepository");
    }

    [Fact]
    public void CouponService_ApplyCouponAsync_is_a_real_action_in_the_coupons_module()
    {
        var graph = BuildRealGraph();
        var actionId = "ECommerceApp.Application.Sales.Coupons.Services.CouponService.ApplyCouponAsync";

        Assert.Contains(graph.Nodes, node => node.Label == "Action" && node.Id == actionId);
        Assert.Contains(graph.Edges, edge =>
            edge.Type == "CONTAINS" &&
            edge.SourceId == "Coupons" &&
            edge.TargetLabel == "Action" &&
            edge.TargetId == actionId);
    }

    [Fact]
    public void Full_graph_has_expected_lower_bounds_and_zero_unknown_label_warnings()
    {
        var graph = BuildRealGraph();
        var report = GraphValidator.Validate(
            graph,
            OntologyLoader.Load(Path.Combine(FindRepositoryRoot(), "tools", "kg", "seed", "ontology.json")));

        Assert.True(graph.Nodes.Count(node => node.Label == "Entity") >= 30);
        Assert.True(graph.Nodes.Count(node => node.Label == "Repository") >= 25);
        Assert.True(graph.Nodes.Count(node => node.Label == "Action") >= 150);
        Assert.Empty(report.Warnings);
    }

    private static Graph BuildRealGraph()
    {
        var root = FindRepositoryRoot();
        var resolver = new ModuleResolver();
        var graph = SpineCatalog.Create();
        var symbols = DomainSymbolIndex.Build(Path.Combine(root, "ECommerceApp.Domain"));
        var entity = new EntityParser(resolver).Parse(Path.Combine(root, "ECommerceApp.Infrastructure"), symbols);
        entity.Graph.MergeInto(graph);
        var repository = new RepositoryParser(resolver).Parse(
            Path.Combine(root, "ECommerceApp.Domain"),
            graph.Nodes.Where(node => node.Label == "Entity").ToList());
        repository.Graph.MergeInto(graph);
        var action = new ActionParser(resolver).Parse(Path.Combine(root, "ECommerceApp.Application"));
        action.Graph.MergeInto(graph);

        var report = GraphValidator.Validate(
            graph,
            OntologyLoader.Load(Path.Combine(root, "tools", "kg", "seed", "ontology.json")));
        Assert.Empty(report.Errors);
        return graph;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ECommerceApp.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ECommerceApp repository root.");
    }
}
