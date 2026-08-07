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
    public void Real_graph_has_atomic_roles_and_trusted_policy_only()
    {
        var graph = BuildRealGraph();

        Assert.Equal(["Administrator", "Manager", "Service"],
            graph.Nodes.Where(node => node.Label == "Role").Select(node => node.Id).OrderBy(id => id));
        Assert.Equal(["TrustedApiUser"],
            graph.Nodes.Where(node => node.Label == "Policy").Select(node => node.Id));
        Assert.DoesNotContain(graph.Nodes, node => node.Id is "ManagingRole" or "MaintenanceRole" or "StorefrontIndex");
    }

    [Fact]
    public void Real_graph_intersects_class_and_method_roles()
    {
        var graph = BuildRealGraph();
        var stockEdges = graph.Edges
            .Where(edge => edge.Type == "GOVERNED_BY" && edge.SourceId.Contains("StockController", StringComparison.Ordinal))
            .ToArray();

        Assert.DoesNotContain(stockEdges, edge => edge.SourceId.EndsWith("Adjust", StringComparison.Ordinal) && edge.TargetId == "Service");
        Assert.DoesNotContain(stockEdges, edge => edge.SourceId.EndsWith("Release", StringComparison.Ordinal) && edge.TargetId == "Service");
        Assert.DoesNotContain(stockEdges, edge => edge.SourceId.EndsWith("Confirm", StringComparison.Ordinal) && edge.TargetId == "Service");
        Assert.DoesNotContain(stockEdges, edge => edge.SourceId.EndsWith("Withdraw", StringComparison.Ordinal) && edge.TargetId == "Service");
        foreach (var method in new[] { "Adjust", "Release", "Confirm", "Withdraw" })
        {
            Assert.Contains(stockEdges, edge => edge.SourceId.EndsWith(method, StringComparison.Ordinal) && edge.TargetId == "Administrator");
            Assert.Contains(stockEdges, edge => edge.SourceId.EndsWith(method, StringComparison.Ordinal) && edge.TargetId == "Manager");
        }
    }

    [Fact]
    public void Real_graph_has_five_policy_governance_edges()
    {
        var graph = BuildRealGraph();
        var policyEdges = graph.Edges
            .Where(edge => edge.Type == "GOVERNED_BY" && edge.TargetLabel == "Policy" && edge.TargetId == "TrustedApiUser")
            .ToArray();

        Assert.Equal(5, policyEdges.Length);
        Assert.All(policyEdges, edge =>
            Assert.True(edge.SourceId.Contains("CartController", StringComparison.Ordinal) ||
                        edge.SourceId.Contains("CheckoutController", StringComparison.Ordinal)));
    }

    [Fact]
    public void Real_graph_has_no_role_policy_source_alignment_warnings()
    {
        BuildRealGraph(out var rolePolicyWarnings);

        Assert.DoesNotContain(rolePolicyWarnings, warning => warning.Contains("has no matching", StringComparison.Ordinal));
    }

    [Fact]
    public void Real_graph_preserves_overload_suffix_and_imperative_authorization_gap()
    {
        var graph = BuildRealGraph();
        var orderEdges = graph.Edges
            .Where(edge => edge.Type == "GOVERNED_BY" && edge.SourceId.Contains("Web.Areas.Sales.Controllers.OrdersController.Index", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(6, orderEdges.Length);
        Assert.Equal(3, orderEdges.Count(edge => edge.SourceId.EndsWith("Index", StringComparison.Ordinal)));
        Assert.Equal(3, orderEdges.Count(edge => edge.SourceId.EndsWith("Index#2", StringComparison.Ordinal)));
        Assert.DoesNotContain(graph.Edges, edge => edge.Type == "GOVERNED_BY" && edge.SourceId.Contains("API.Controllers.Sales.OrdersController.GetById", StringComparison.Ordinal));
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

    [Fact]
    public void Real_graph_has_message_and_handler_coverage()
    {
        var graph = BuildRealGraph();

        Assert.True(graph.Nodes.Count(node => node.Label == "Message") >= 24);
        Assert.True(graph.Nodes.Count(node => node.Label == "MessageHandler") >= 50);
        Assert.DoesNotContain(graph.Nodes, node => node.Label == "Job");
        Assert.DoesNotContain(graph.Nodes, node => node.Label == "MessageHandler" && node.Id.EndsWith("StockAdjustmentJob", StringComparison.Ordinal));
        Assert.All(
            graph.Edges.Where(edge => edge.Type == "PUBLISHES"),
            edge => Assert.Equal("Action", edge.SourceLabel));
        Assert.All(
            graph.Edges.Where(edge => edge.Type == "HANDLED_BY" && edge.SourceLabel == "Message"),
            edge =>
            {
                Assert.Equal("Message", edge.SourceLabel);
                Assert.Equal("MessageHandler", edge.TargetLabel);
            });
        Assert.DoesNotContain(graph.Nodes, node =>
            node.Label == "Message" && node.Id.StartsWith("ECommerceApp.Domain", StringComparison.Ordinal));
    }

    [Fact]
    public void Real_graph_has_no_message_source_alignment_warnings()
    {
        BuildRealGraph(out _, out var messageWarnings);

        Assert.DoesNotContain(messageWarnings, warning => warning.Contains("has no matching Action node", StringComparison.Ordinal));
    }

    /// <summary>
    /// The name-collision case this phase exists for. Two `RefundApproved` records live in sibling
    /// namespaces; only the Fulfillment one is registered, and every handler means that one —
    /// including `PaymentRefundApprovedHandler`, which sits in the *other* namespace's module.
    /// </summary>
    [Fact]
    public void Real_graph_separates_the_two_RefundApproved_messages()
    {
        var graph = BuildRealGraph();
        const string fulfillment = "ECommerceApp.Application.Sales.Fulfillment.Messages.RefundApproved";
        const string payments = "ECommerceApp.Application.Sales.Payments.Messages.RefundApproved";

        Assert.Equal("fulfillment.refund.approved",
            Assert.Single(graph.Nodes, node => node.Label == "Message" && node.Id == fulfillment).Properties["key"]);
        Assert.Null(Assert.Single(graph.Nodes, node => node.Label == "Message" && node.Id == payments).Properties["key"]);
        Assert.DoesNotContain(graph.Edges, edge => edge.SourceId == payments || edge.TargetId == payments);

        string[] handlers =
        [
            "ECommerceApp.Application.Inventory.Availability.Handlers.RefundApprovedHandler",
            "ECommerceApp.Application.Sales.Orders.Handlers.OrderRefundApprovedHandler",
            "ECommerceApp.Application.Sales.Payments.Handlers.PaymentRefundApprovedHandler",
            "ECommerceApp.Application.Supporting.Communication.Handlers.RefundApprovedEmailHandler",
            "ECommerceApp.Application.Supporting.Communication.Handlers.RefundApprovedNotificationHandler"
        ];
        foreach (var handler in handlers)
        {
            Assert.Contains(graph.Edges, edge =>
                edge.Type == "HANDLED_BY" && edge.SourceId == fulfillment && edge.TargetId == handler);
        }
    }

    [Fact]
    public void Real_graph_registry_keys_are_resolved_through_using_aliases()
    {
        var graph = BuildRealGraph();

        // The two aliased registrations must land on the real FQCNs, never on a node named after
        // the alias.
        Assert.DoesNotContain(graph.Nodes, node =>
            node.Id.EndsWith("FulfillmentRefundApproved", StringComparison.Ordinal) ||
            node.Id.EndsWith("FulfillmentRefundRejected", StringComparison.Ordinal));
        Assert.Equal(
            [
                "ECommerceApp.Application.Catalog.Products.Messages.CategoryNameChanged",
                "ECommerceApp.Application.Catalog.Products.Messages.ProductDiscontinued",
                "ECommerceApp.Application.Catalog.Products.Messages.ProductNameChanged",
                "ECommerceApp.Application.Catalog.Products.Messages.TagNameChanged",
                "ECommerceApp.Application.Sales.Orders.Messages.OrderRequiresAttention",
                "ECommerceApp.Application.Sales.Payments.Messages.RefundApproved"
            ],
            graph.Nodes
                .Where(node => node.Label == "Message" && node.Properties["key"] is null)
                .Select(node => node.Id)
                .OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public void Real_graph_counts_multi_interface_handlers_once()
    {
        var graph = BuildRealGraph();

        foreach (var handler in new[]
        {
            "ECommerceApp.Application.Catalog.Products.Handlers.ProductCacheInvalidationHandler",
            "ECommerceApp.Application.Presale.Checkout.Handlers.ProductDetailsCacheInvalidationHandler"
        })
        {
            Assert.Single(graph.Nodes, node => node.Label == "MessageHandler" && node.Id == handler);
            Assert.Equal(4, graph.Edges.Count(edge => edge.Type == "HANDLED_BY" && edge.TargetId == handler));
        }
    }

    /// <summary>
    /// `ProductDiscontinued` is unregistered, so it carries `key: null` — but it keeps its handler
    /// edges, because three handlers genuinely run on it. Nothing in `Application` constructs it,
    /// so it has no inbound publish. The `ECommerceApp.Domain` type of the same name must not leak
    /// in through the simple-name lookup.
    /// </summary>
    [Fact]
    public void Real_graph_keeps_handler_edges_for_the_unregistered_ProductDiscontinued()
    {
        var graph = BuildRealGraph();
        const string id = "ECommerceApp.Application.Catalog.Products.Messages.ProductDiscontinued";

        Assert.Null(Assert.Single(graph.Nodes, node => node.Label == "Message" && node.Id == id).Properties["key"]);
        Assert.Equal(
            [
                "ECommerceApp.Application.Catalog.Products.Handlers.ProductCacheInvalidationHandler",
                "ECommerceApp.Application.Inventory.Availability.Handlers.ProductDiscontinuedHandler",
                "ECommerceApp.Application.Presale.Checkout.Handlers.ProductDetailsCacheInvalidationHandler"
            ],
            graph.Edges
                .Where(edge => edge.Type == "HANDLED_BY" && edge.SourceId == id)
                .Select(edge => edge.TargetId)
                .OrderBy(target => target, StringComparer.Ordinal));
        Assert.DoesNotContain(graph.Edges, edge => edge.Type == "PUBLISHES" && edge.TargetId == id);
    }

    [Fact]
    public void Real_graph_marks_id_aware_handlers()
    {
        var graph = BuildRealGraph();
        var handlers = graph.Nodes.Where(node => node.Label == "MessageHandler").ToArray();

        Assert.True(handlers.Count(node => node.Properties["idAware"] is true) >= 22);
        Assert.True(handlers.Count(node => node.Properties["idAware"] is false) >= 27);
        Assert.All(handlers, node => Assert.IsType<bool>(node.Properties["idAware"]));
        Assert.True(Assert.Single(handlers, node => node.Id == "ECommerceApp.Application.Inventory.Availability.Handlers.RefundApprovedHandler").Properties["idAware"] is true);
        Assert.True(Assert.Single(handlers, node => node.Id == "ECommerceApp.Application.Sales.Payments.Handlers.PaymentRefundApprovedHandler").Properties["idAware"] is false);
    }

    [Fact]
    public void Real_graph_publishes_coupon_and_refund_messages_from_their_services()
    {
        var graph = BuildRealGraph();

        (string Action, string Message, string? Key)[] expected =
        [
            ("Sales.Coupons.Services.CouponService.ApplyCouponAsync", "Sales.Coupons.Messages.CouponApplied", "coupons.coupon.applied"),
            ("Sales.Coupons.Services.CouponService.ApplyCouponAsync", "Sales.Coupons.Messages.OrderPriceAdjusted", "coupons.order-price.adjusted"),
            ("Sales.Coupons.Services.CouponService.RemoveCouponAsync", "Sales.Coupons.Messages.CouponRemovedFromOrder", "coupons.coupon.removed-from-order"),
            ("Sales.Fulfillment.Services.RefundService.ApproveRefundAsync", "Sales.Fulfillment.Messages.RefundApproved", "fulfillment.refund.approved"),
            ("Sales.Fulfillment.Services.RefundService.RejectRefundAsync", "Sales.Fulfillment.Messages.RefundRejected", "fulfillment.refund.rejected")
        ];

        foreach (var (action, message, key) in expected)
        {
            var actionId = "ECommerceApp.Application." + action;
            var messageId = "ECommerceApp.Application." + message;
            Assert.Contains(graph.Edges, edge =>
                edge.Type == "PUBLISHES" && edge.SourceId == actionId && edge.TargetId == messageId);
            Assert.Equal(key, Assert.Single(graph.Nodes, node => node.Label == "Message" && node.Id == messageId).Properties["key"]);
        }

        // `new RefundApprovedItem(...)` in the same method is not an `IMessage` type and must not
        // become a publish.
        Assert.DoesNotContain(graph.Edges, edge => edge.Type == "PUBLISHES" && edge.TargetId.EndsWith("RefundApprovedItem", StringComparison.Ordinal));
    }

    /// <summary>
    /// `OrderService` declares the message in a local variable and enqueues the identifier, not a
    /// `new` expression. Missing this edge is the difference between seeing order placement in the
    /// graph and not seeing it at all.
    /// </summary>
    [Fact]
    public void Real_graph_follows_a_message_published_through_a_local_variable()
    {
        var graph = BuildRealGraph();

        Assert.Contains(graph.Edges, edge =>
            edge.Type == "PUBLISHES" &&
            edge.SourceId == "ECommerceApp.Application.Sales.Orders.Services.OrderService.PlaceOrderAsync" &&
            edge.TargetId == "ECommerceApp.Application.Sales.Orders.Messages.OrderPlaced");
    }

    /// <summary>
    /// The three `StockReconciliationRequired` publishes live in handlers, and the two job publishes
    /// in `*Job.cs`. Neither can originate a `PUBLISHES` edge, and neither is a parse failure — so
    /// both are silent. Pinned so the silence stays deliberate. See `MessageParser`'s summary.
    /// </summary>
    [Fact]
    public void Real_graph_omits_handler_and_job_sourced_publishes_without_warning()
    {
        var graph = BuildRealGraph(out _, out var messageWarnings);

        Assert.DoesNotContain(graph.Edges, edge =>
            edge.Type == "PUBLISHES" && edge.TargetId.EndsWith("StockReconciliationRequired", StringComparison.Ordinal));
        Assert.DoesNotContain(graph.Edges, edge =>
            edge.Type == "PUBLISHES" && edge.TargetId.EndsWith("PaymentExpired", StringComparison.Ordinal));
        Assert.DoesNotContain(messageWarnings, warning =>
            warning.Contains("StockReconciliationRequired", StringComparison.Ordinal) ||
            warning.Contains("PaymentExpired", StringComparison.Ordinal));
    }

    [Fact]
    public void Real_graph_has_exact_query_contracts_and_handlers()
    {
        var graph = BuildRealGraph();

        Assert.Equal(
            [
                "ECommerceApp.Application.Messaging.CompletedOrderCountQuery",
                "ECommerceApp.Application.Messaging.OrderExistsQuery",
                "ECommerceApp.Application.Messaging.StockAvailableQuery"
            ],
            graph.Nodes.Where(node => node.Label == "Query").Select(node => node.Id).OrderBy(id => id, StringComparer.Ordinal));
        Assert.Equal(
            [
                "ECommerceApp.Infrastructure.Inventory.Handlers.StockAvailableQueryHandler",
                "ECommerceApp.Infrastructure.Sales.Orders.Handlers.CompletedOrderCountQueryHandler",
                "ECommerceApp.Infrastructure.Sales.Orders.Handlers.OrderExistsQueryHandler"
            ],
            graph.Nodes.Where(node => node.Label == "QueryHandler").Select(node => node.Id).OrderBy(id => id, StringComparer.Ordinal));
        Assert.DoesNotContain(graph.Nodes, node => node.Label == "QueryHandler" && node.Id.StartsWith("ECommerceApp.Application.", StringComparison.Ordinal));
    }

    [Fact]
    public void Real_graph_has_query_handler_edges_and_result_types()
    {
        var graph = BuildRealGraph();

        Assert.Equal(3, graph.Edges.Count(edge => edge.Type == "HANDLED_BY" && edge.SourceLabel == "Query" && edge.TargetLabel == "QueryHandler"));
        Assert.Equal(3, graph.Edges.Count(edge => edge.Type == "CONTAINS" && edge.TargetLabel == "QueryHandler"));
        Assert.Equal(2, graph.Edges.Count(edge => edge.Type == "CONTAINS" && edge.SourceId == "Orders" && edge.TargetLabel == "QueryHandler"));
        Assert.Single(graph.Edges, edge => edge.Type == "CONTAINS" && edge.SourceId == "Inventory" && edge.TargetLabel == "QueryHandler");
        Assert.Equal("bool", Assert.Single(graph.Nodes, node => node.Id.EndsWith("OrderExistsQuery", StringComparison.Ordinal)).Properties["resultType"]);
        Assert.Equal("int", Assert.Single(graph.Nodes, node => node.Id.EndsWith("CompletedOrderCountQuery", StringComparison.Ordinal)).Properties["resultType"]);
        Assert.Equal("bool", Assert.Single(graph.Nodes, node => node.Id.EndsWith("StockAvailableQuery", StringComparison.Ordinal)).Properties["resultType"]);
    }

    [Fact]
    public void Real_graph_pins_query_uses_coverage_gap_and_no_query_containment()
    {
        var graph = BuildRealGraph();

        Assert.Equal(3, graph.Edges.Count(edge => edge.Type == "USES" && edge.TargetLabel == "Query"));
        Assert.Equal(3, graph.Edges.Count(edge => edge.Type == "USES" && edge.TargetId == "ECommerceApp.Application.Messaging.OrderExistsQuery"));
        Assert.DoesNotContain(graph.Edges, edge => edge.Type == "USES" && edge.TargetId != "ECommerceApp.Application.Messaging.OrderExistsQuery");
        Assert.DoesNotContain(graph.Edges, edge => edge.Type == "CONTAINS" && edge.TargetLabel == "Query");
        Assert.DoesNotContain(graph.Nodes, node => node.Label == "Job");
        Assert.DoesNotContain(graph.Edges, edge => edge.Type == "SCHEDULES");
    }

    private static Graph BuildRealGraph() => BuildRealGraph(out _, out _);

    private static Graph BuildRealGraph(out IReadOnlyList<string> rolePolicyWarnings) =>
        BuildRealGraph(out rolePolicyWarnings, out _);

    private static Graph BuildRealGraph(
        out IReadOnlyList<string> rolePolicyWarnings,
        out IReadOnlyList<string> messageWarnings)
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
        // Message runs after Action (PUBLISHES targets Action ids) and MessageHandler after Message
        // (HANDLED_BY targets Message ids) — the same order `CliRunner` uses.
        var message = new MessageParser().Parse(Path.Combine(root, "ECommerceApp.Application"), action.Graph.Nodes);
        message.Graph.MergeInto(graph);
        messageWarnings = message.Warnings;
        var messageHandler = new MessageHandlerParser(resolver).Parse(
            Path.Combine(root, "ECommerceApp.Application"),
            message.Graph.Nodes);
        messageHandler.Graph.MergeInto(graph);
        var query = new QueryParser().Parse(
            Path.Combine(root, "ECommerceApp.Application"),
            graph.Nodes.Where(node => node.Label == "Action").ToList());
        query.Graph.MergeInto(graph);
        var queryHandler = new QueryHandlerParser(resolver).Parse(
            Path.Combine(root, "ECommerceApp.Infrastructure"),
            query.Graph.Nodes);
        queryHandler.Graph.MergeInto(graph);
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
        var rolePolicy = new RolePolicyParser().Parse(
            Path.Combine(root, "ECommerceApp.Application"),
            Path.Combine(root, "ECommerceApp.API"),
            Path.Combine(root, "ECommerceApp.Web"),
            graph.Nodes.Where(node => node.Label == "Endpoint").ToList(),
            graph.Nodes.Where(node => node.Label == "Page").ToList());
        rolePolicy.Graph.MergeInto(graph);
        rolePolicyWarnings = rolePolicy.Warnings;

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
