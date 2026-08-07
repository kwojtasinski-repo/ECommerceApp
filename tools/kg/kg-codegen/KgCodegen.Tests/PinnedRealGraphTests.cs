using KgCodegen.Core.Model;
using KgCodegen.Core.Ontology;
using KgCodegen.Core.Parsing;
using KgCodegen.Core.Spine;
using KgCodegen.Core.Validation;

namespace KgCodegen.Tests;

public sealed class PinnedRealGraphTests
{
    [Fact]
    public void Spine_has_exactly_two_hosts()
    {
        var graph = BuildRealGraph();

        Assert.Equal(2, graph.Nodes.Count(node => node.Label == "Host"));
        Assert.Contains(graph.Nodes, node => node.Label == "Host" && node.Id == "ApiHost");
        Assert.Contains(graph.Nodes, node => node.Label == "Host" && node.Id == "WebHost");
    }

    [Fact]
    public void Real_graph_has_endpoint_and_page_coverage_for_orders()
    {
        var graph = BuildRealGraph();

        Assert.True(graph.Nodes.Count(node => node.Label == "Endpoint") >= 40);
        Assert.True(graph.Nodes.Count(node => node.Label == "Page") >= 170);
        Assert.Contains(graph.Edges, edge =>
            edge.Type == "EXPOSED_BY" &&
            edge.SourceId == "ECommerceApp.Application.Sales.Orders.Services.OrderService.GetOrderDetailsAsync" &&
            edge.TargetLabel == "Endpoint");
        Assert.Contains(graph.Edges, edge =>
            edge.Type == "EXPOSED_BY" &&
            edge.SourceId == "ECommerceApp.Application.Sales.Orders.Services.OrderService.GetOrderDetailsAsync" &&
            edge.TargetLabel == "Page");
    }

    [Fact]
    public void Storefront_api_controller_is_parsed_via_the_direct_ApiController_branch()
    {
        // StorefrontController is the only API controller declaring [ApiController] on itself
        // instead of inheriting it from BaseController — it is the sole cover for that branch.
        var graph = BuildRealGraph();
        var endpoint = Assert.Single(
            graph.Nodes,
            node => node.Label == "Endpoint" && node.Id == "ECommerceApp.API.Controllers.Presale.StorefrontController.GetProducts");

        Assert.Equal("GET", endpoint.Properties["httpMethod"]);
        Assert.Equal("api/storefront/products", endpoint.Properties["route"]);
        Assert.Contains(graph.Edges, edge =>
            edge.Type == "CONTAINS" && edge.SourceId == "ApiHost" && edge.TargetId == endpoint.Id);
        Assert.Contains(graph.Nodes, node =>
            node.Label == "Endpoint" && node.Id == "ECommerceApp.API.Controllers.Presale.StorefrontController.GetProductsByTag");
    }

    [Fact]
    public void Decorator_only_service_resolves_to_the_decorator_class()
    {
        // ICatalogNavigationService has no CatalogNavigationService class, so the I-prefix convention
        // misses and the edge has to come from the interface-implementation fallback.
        var graph = BuildRealGraph();

        Assert.Contains(graph.Edges, edge =>
            edge.Type == "EXPOSED_BY" &&
            edge.SourceId == "ECommerceApp.Application.Catalog.Products.Services.CachedCatalogNavigationService.GetAllCategories" &&
            edge.TargetId == "ECommerceApp.Web.Areas.Presale.Controllers.StorefrontController.Index");
    }

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
        var applicationSymbols = DomainSymbolIndex.Build(Path.Combine(root, "ECommerceApp.Application"));
        var endpoint = new EndpointParser().Parse(
            Path.Combine(root, "ECommerceApp.API"),
            applicationSymbols,
            graph.Nodes.Where(node => node.Label == "Action").ToList());
        endpoint.Graph.MergeInto(graph);
        var page = new PageParser().Parse(
            Path.Combine(root, "ECommerceApp.Web"),
            applicationSymbols,
            graph.Nodes.Where(node => node.Label == "Action").ToList());
        page.Graph.MergeInto(graph);

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
