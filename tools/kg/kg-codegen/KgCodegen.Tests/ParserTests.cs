using KgCodegen.Core.Parsing;
using KgCodegen.Core.Model;

namespace KgCodegen.Tests;

public sealed class ParserTests
{
    [Fact]
    public void ModuleResolver_prefers_longest_logical_path()
    {
        var resolver = new ModuleResolver(new Dictionary<string, string> { ["A"] = "Sales", ["B"] = "Sales/Orders" });
        Assert.Equal("B", resolver.Resolve("Sales/Orders/Services/OrderService.cs"));
    }

    [Fact]
    public void ActionParser_emits_only_public_methods_from_service()
    {
        var root = Path.Combine(Path.GetTempPath(), "kg-actions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Sales", "Orders"));
        File.WriteAllText(Path.Combine(root, "Sales", "Orders", "OrderService.cs"), """
            namespace Demo;
            public sealed class OrderService
            {
                public void DoWork() { }
                private void Hidden() { }
            }
            """);
        var result = new ActionParser(new ModuleResolver(new Dictionary<string, string> { ["Orders"] = "Sales/Orders" })).Parse(root);
        Assert.Single(result.Graph.Nodes);
        Assert.EndsWith("DoWork", result.Graph.Nodes[0].Id);
        Directory.Delete(root, true);
    }

    [Fact]
    public void EntityParser_reads_table_only_for_configuration_marker()
    {
        var root = Path.Combine(Path.GetTempPath(), "kg-entities-" + Guid.NewGuid().ToString("N"));
        var configDir = Path.Combine(root, "Sales", "Orders", "Configurations");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "OrderConfiguration.cs"), """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            namespace Demo;
            public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
            {
                public void Configure(EntityTypeBuilder<Order> builder) => builder.ToTable("Orders");
            }
            public sealed class Order { }
            """);
        var domain = Path.Combine(root, "domain");
        Directory.CreateDirectory(domain);
        File.WriteAllText(Path.Combine(domain, "Order.cs"), "namespace Demo; public sealed class Order { }");
        var result = new EntityParser(new ModuleResolver(new Dictionary<string, string> { ["Orders"] = "Sales/Orders" }))
            .Parse(root, DomainSymbolIndex.Build(domain));
        Assert.Single(result.Graph.Nodes);
        Assert.Equal("Orders", result.Graph.Nodes[0].Properties["table"]);
        Directory.Delete(root, true);
    }

    [Fact]
    public void RepositoryParser_links_referenced_entities()
    {
        var root = Path.Combine(Path.GetTempPath(), "kg-repos-" + Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(root, "Sales", "Orders");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "IOrderRepository.cs"), "namespace Demo; public interface IOrderRepository { Order? Get(); }");
        var entities = new[] { new CypherNode("Entity", "Demo.Order", new Dictionary<string, object?>()) };
        var result = new RepositoryParser(new ModuleResolver(new Dictionary<string, string> { ["Orders"] = "Sales/Orders" })).Parse(root, entities);
        Assert.Single(result.Graph.Nodes);
        Assert.Contains(result.Graph.Edges, edge => edge.Type == "PERSISTED_BY");
        Directory.Delete(root, true);
    }

    [Fact]
    public void RepositoryParser_does_not_link_entity_whose_name_is_only_a_substring_of_the_referenced_type()
    {
        // Regression test: ECommerceApp really has Coupon / CouponUsed / CouponApplicationRecord
        // sharing a name prefix. A repository that only ever references CouponUsed must not be
        // linked to the unrelated Coupon entity just because "Coupon" is a substring of "CouponUsed".
        var root = Path.Combine(Path.GetTempPath(), "kg-repos-substring-" + Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(root, "Sales", "Coupons");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "ICouponUsedRepository.cs"),
            "namespace Demo; public interface ICouponUsedRepository { CouponUsed? FindByOrderId(int id); }");
        var entities = new[] { new CypherNode("Entity", "Demo.Coupon", new Dictionary<string, object?>()) };
        var result = new RepositoryParser(new ModuleResolver(new Dictionary<string, string> { ["Coupons"] = "Sales/Coupons" })).Parse(root, entities);
        Assert.DoesNotContain(result.Graph.Edges, edge => edge.Type == "PERSISTED_BY");
        Directory.Delete(root, true);
    }

    [Fact]
    public void EndpointParser_emits_route_and_resolves_service_action()
    {
        var root = Path.Combine(Path.GetTempPath(), "kg-endpoints-" + Guid.NewGuid().ToString("N"));
        var apiRoot = Path.Combine(root, "api");
        var applicationRoot = Path.Combine(root, "application");
        Directory.CreateDirectory(Path.Combine(apiRoot, "Controllers"));
        Directory.CreateDirectory(applicationRoot);
        File.WriteAllText(Path.Combine(apiRoot, "Controllers", "OrdersController.cs"), """
            using Microsoft.AspNetCore.Mvc;
            namespace Demo.Api;
            [ApiController]
            [Route("api/orders")]
            public sealed class OrdersController : ControllerBase
            {
                private readonly IOrderService _orders;
                public OrdersController(IOrderService orders) => _orders = orders;
                [HttpGet("{id}")]
                public IActionResult Get(int id) { _orders.Get(id); return Ok(); }
            }
            """);
        File.WriteAllText(Path.Combine(applicationRoot, "OrderService.cs"), """
            namespace Demo.Application;
            public sealed class OrderService { public void Get(int id) { } }
            """);

        var actions = new[] { new CypherNode("Action", "Demo.Application.OrderService.Get", new Dictionary<string, object?>()) };
        var result = new EndpointParser().Parse(apiRoot, DomainSymbolIndex.Build(applicationRoot), actions);

        var endpoint = Assert.Single(result.Graph.Nodes, node => node.Label == "Endpoint");
        Assert.Equal("GET", endpoint.Properties["httpMethod"]);
        Assert.Equal("api/orders/{id}", endpoint.Properties["route"]);
        Assert.Contains(result.Graph.Edges, edge => edge.Type == "EXPOSED_BY" && edge.SourceId == "Demo.Application.OrderService.Get");
        Assert.Empty(result.Warnings);
        Directory.Delete(root, true);
    }

    [Fact]
    public void PageParser_warns_and_does_not_fabricate_edge_for_unresolved_interface()
    {
        var root = Path.Combine(Path.GetTempPath(), "kg-pages-" + Guid.NewGuid().ToString("N"));
        var webRoot = Path.Combine(root, "web");
        var applicationRoot = Path.Combine(root, "application");
        Directory.CreateDirectory(Path.Combine(webRoot, "Areas", "Sales", "Controllers"));
        Directory.CreateDirectory(applicationRoot);
        File.WriteAllText(Path.Combine(webRoot, "Areas", "Sales", "Controllers", "OrdersController.cs"), """
            using Microsoft.AspNetCore.Mvc;
            namespace Demo.Web;
            [Route("sales/orders")]
            public sealed class OrdersController : Controller
            {
                private readonly IMissingService _service;
                public OrdersController(IMissingService service) => _service = service;
                [HttpGet("details")]
                public IActionResult Details() { _service.Load(); return View(); }
            }
            """);
        File.WriteAllText(Path.Combine(applicationRoot, "RealService.cs"), """
            namespace Demo.Application;
            public sealed class RealService { public void Load() { } }
            """);

        var actions = new[] { new CypherNode("Action", "Demo.Application.RealService.Load", new Dictionary<string, object?>()) };
        var result = new PageParser().Parse(webRoot, DomainSymbolIndex.Build(applicationRoot), actions);

        var page = Assert.Single(result.Graph.Nodes, node => node.Label == "Page");
        Assert.Equal("GET", page.Properties["httpMethod"]);
        Assert.Equal("sales/orders/details", page.Properties["route"]);
        Assert.Contains(result.Warnings, warning => warning.Contains("IMissingService.Load", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Graph.Edges, edge => edge.Type == "EXPOSED_BY");
        Directory.Delete(root, true);
    }

    [Fact]
    public void PageParser_resolves_decorator_when_service_name_breaks_the_I_prefix_convention()
    {
        // Real case: ICatalogNavigationService has no CatalogNavigationService class — its only
        // implementation is the CachedCatalogNavigationService decorator.
        var root = Path.Combine(Path.GetTempPath(), "kg-decorator-" + Guid.NewGuid().ToString("N"));
        var webRoot = Path.Combine(root, "web");
        var applicationRoot = Path.Combine(root, "application");
        Directory.CreateDirectory(Path.Combine(webRoot, "Controllers"));
        Directory.CreateDirectory(applicationRoot);
        File.WriteAllText(Path.Combine(webRoot, "Controllers", "StorefrontController.cs"), """
            using Microsoft.AspNetCore.Mvc;
            namespace Demo.Web;
            [Route("offers")]
            public sealed class StorefrontController : Controller
            {
                private readonly INavigationService _navigation;
                public StorefrontController(INavigationService navigation) => _navigation = navigation;
                [HttpGet("")]
                public IActionResult Index() { _navigation.GetAllCategories(); return View(); }
            }
            """);
        File.WriteAllText(Path.Combine(applicationRoot, "CachedNavigationService.cs"), """
            namespace Demo.Application;
            internal sealed class CachedNavigationService : INavigationService { public void GetAllCategories() { } }
            """);

        var actions = new[] { new CypherNode("Action", "Demo.Application.CachedNavigationService.GetAllCategories", new Dictionary<string, object?>()) };
        var result = new PageParser().Parse(webRoot, DomainSymbolIndex.Build(applicationRoot), actions);

        var page = Assert.Single(result.Graph.Nodes, node => node.Label == "Page");
        Assert.Equal("offers", page.Properties["route"]);
        Assert.Contains(result.Graph.Edges, edge =>
            edge.Type == "EXPOSED_BY" && edge.SourceId == "Demo.Application.CachedNavigationService.GetAllCategories");
        Assert.Empty(result.Warnings);
        Directory.Delete(root, true);
    }

    [Fact]
    public void PageParser_warns_and_does_not_pick_one_when_two_classes_implement_the_service()
    {
        var root = Path.Combine(Path.GetTempPath(), "kg-ambiguous-" + Guid.NewGuid().ToString("N"));
        var webRoot = Path.Combine(root, "web");
        var applicationRoot = Path.Combine(root, "application");
        Directory.CreateDirectory(Path.Combine(webRoot, "Controllers"));
        Directory.CreateDirectory(applicationRoot);
        File.WriteAllText(Path.Combine(webRoot, "Controllers", "MailController.cs"), """
            using Microsoft.AspNetCore.Mvc;
            namespace Demo.Web;
            [Route("mail")]
            public sealed class MailController : Controller
            {
                private readonly IEmailService _email;
                public MailController(IEmailService email) => _email = email;
                [HttpGet("send")]
                public IActionResult Send() { _email.Send(); return View(); }
            }
            """);
        File.WriteAllText(Path.Combine(applicationRoot, "LoggingEmailService.cs"), """
            namespace Demo.Application;
            internal sealed class LoggingEmailService : IEmailService { public void Send() { } }
            """);
        File.WriteAllText(Path.Combine(applicationRoot, "SmtpEmailService.cs"), """
            namespace Demo.Application;
            internal sealed class SmtpEmailService : IEmailService { public void Send() { } }
            """);

        var actions = new[]
        {
            new CypherNode("Action", "Demo.Application.LoggingEmailService.Send", new Dictionary<string, object?>()),
            new CypherNode("Action", "Demo.Application.SmtpEmailService.Send", new Dictionary<string, object?>())
        };
        var result = new PageParser().Parse(webRoot, DomainSymbolIndex.Build(applicationRoot), actions);

        Assert.DoesNotContain(result.Graph.Edges, edge => edge.Type == "EXPOSED_BY");
        Assert.Contains(result.Warnings, warning =>
            warning.Contains("IEmailService.Send", StringComparison.Ordinal) &&
            warning.Contains("more than one type declares that name", StringComparison.Ordinal));
        Directory.Delete(root, true);
    }
}
