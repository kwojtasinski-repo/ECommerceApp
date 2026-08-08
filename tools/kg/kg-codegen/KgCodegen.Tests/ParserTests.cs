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

    [Fact]
    public void AtomicRoleCatalog_reads_declared_role_constants_instead_of_hardcoding_names()
    {
        var root = Path.Combine(Path.GetTempPath(), "kg-role-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "UserPermissions.cs"), "public static class UserPermissions { public static class Roles { public const string Administrator = \"Administrator\"; public const string Customer = \"Customer\"; } }");

        var catalog = AtomicRoleCatalog.Build(root);

        Assert.Equal(2, catalog.Count);
        Assert.Equal("Customer", catalog.ResolveConstant("Customer"));
        Assert.True(catalog.IsDeclaredValue("Customer"));
        Assert.False(catalog.IsDeclaredValue("User"));
        Assert.Empty(catalog.Warnings);
        Directory.Delete(root, true);
    }

    [Fact]
    public void RolePolicyParser_splits_alias_and_interpolation_without_alias_nodes()
    {
        var root = CreateRoleFixture(
            "public const string MaintenanceRole = \"Administrator, Manager, Service\";",
            "[Authorize(Roles = MaintenanceRole)] public IActionResult Bare() => Ok();\n[Authorize(Roles = $\"{MaintenanceRole}\")] public IActionResult Interpolated() => Ok();");

        var result = ParseRoleFixture(root.Api, root.Web);

        Assert.Equal(["Administrator", "Manager", "Service"], result.Graph.Nodes.Where(node => node.Label == "Role").Select(node => node.Id).OrderBy(id => id));
        Assert.Equal(6, result.Graph.Edges.Count(edge => edge.Type == "GOVERNED_BY"));
        Assert.DoesNotContain(result.Graph.Nodes, node => node.Id == "MaintenanceRole");
        Directory.Delete(root.Root, true);
    }

    [Fact]
    public void RolePolicyParser_ignores_output_cache_policy_and_bare_authorize()
    {
        var root = CreateRoleFixture(
            "public const string MaintenanceRole = \"Administrator\";",
            "[OutputCache(PolicyName = \"StorefrontIndex\")] public IActionResult Cached() => Ok();\n[Authorize] public IActionResult Authenticated() => Ok();");

        var result = ParseRoleFixture(root.Api, root.Web);

        Assert.Empty(result.Graph.Nodes);
        Assert.Empty(result.Graph.Edges);
        Assert.DoesNotContain(result.Warnings, warning => warning.Contains("Authorize", StringComparison.Ordinal));
        Directory.Delete(root.Root, true);
    }

    [Fact]
    public void RolePolicyParser_resolves_aliases_per_project_and_intersects_levels()
    {
        var root = Path.Combine(Path.GetTempPath(), "kg-role-projects-" + Guid.NewGuid().ToString("N"));
        var api = Path.Combine(root, "api");
        var web = Path.Combine(root, "web");
        Directory.CreateDirectory(Path.Combine(api, "Controllers"));
        Directory.CreateDirectory(Path.Combine(web, "Controllers"));
        var application = Path.Combine(root, "application");
        Directory.CreateDirectory(application);
        File.WriteAllText(Path.Combine(application, "UserPermissions.cs"), "public static class UserPermissions { public static class Roles { public const string Administrator = \"Administrator\"; public const string Manager = \"Manager\"; public const string Service = \"Service\"; } }");
        File.WriteAllText(Path.Combine(api, "Controllers", "BaseController.cs"), "public class BaseController : Microsoft.AspNetCore.Mvc.ControllerBase { public const string MaintenanceRole = \"Administrator, Manager, Service\"; }");
        File.WriteAllText(Path.Combine(web, "Controllers", "BaseController.cs"), "public class BaseController : Microsoft.AspNetCore.Mvc.Controller { public const string MaintenanceRole = \"Administrator, Manager\"; public const string ManagingRole = \"Administrator, Manager\"; }");
        File.WriteAllText(Path.Combine(api, "Controllers", "OrdersController.cs"), "using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; [ApiController] public class OrdersController : BaseController { [Authorize(Roles = MaintenanceRole)] public IActionResult Get() => Ok(); }");
        File.WriteAllText(Path.Combine(web, "Controllers", "StorefrontController.cs"), "using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; public class StorefrontController : BaseController { [Authorize(Roles = MaintenanceRole)] public IActionResult Get() => View(); }");

        var result = ParseRoleFixture(api, web, application);
        var apiId = Assert.Single(result.Graph.Edges.Where(edge => edge.SourceLabel == "Endpoint").Select(edge => edge.SourceId).Distinct(StringComparer.Ordinal));
        var webId = Assert.Single(result.Graph.Edges.Where(edge => edge.SourceLabel == "Page").Select(edge => edge.SourceId).Distinct(StringComparer.Ordinal));
        Assert.Contains(result.Graph.Edges, edge => edge.SourceId == apiId && edge.TargetId == "Service");
        Assert.DoesNotContain(result.Graph.Edges, edge => edge.SourceId == webId && edge.TargetId == "Service");
        Directory.Delete(root, true);
    }

    [Fact]
    public void RolePolicyParser_intersects_class_and_method_role_attributes()
    {
        var root = CreateRoleFixture(
            "public const string MaintenanceRole = \"Administrator, Manager, Service\"; public const string ManagingRole = \"Administrator, Manager\";",
            "[Authorize(Roles = ManagingRole)] public IActionResult Update() => Ok();",
            "[Authorize(Roles = MaintenanceRole)]");

        var result = ParseRoleFixture(root.Api, root.Web);

        Assert.Equal(["Administrator", "Manager"], result.Graph.Edges.Select(edge => edge.TargetId).OrderBy(id => id));
        Assert.DoesNotContain(result.Graph.Edges, edge => edge.TargetId == "Service");
        Assert.Empty(result.Warnings);
        Directory.Delete(root.Root, true);
    }

    [Fact]
    public void RolePolicyParser_warns_for_unknown_alias_without_fabricating_node()
    {
        var root = CreateRoleFixture(
            "public const string MaintenanceRole = \"Administrator\";",
            "[Authorize(Roles = MissingRole)] public IActionResult Get() => Ok();");

        var result = ParseRoleFixture(root.Api, root.Web);

        Assert.Empty(result.Graph.Nodes);
        Assert.Contains(result.Warnings, warning => warning.Contains("MissingRole", StringComparison.Ordinal));
        Directory.Delete(root.Root, true);
    }

    [Fact]
    public void Message_parsers_resolve_registry_aliases_handlers_and_local_publishes()
    {
        var root = Path.Combine(Path.GetTempPath(), "kg-messages-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Messages"));
        Directory.CreateDirectory(Path.Combine(root, "Messaging"));
        Directory.CreateDirectory(Path.Combine(root, "Services", "Handlers"));
        File.WriteAllText(Path.Combine(root, "Messages", "Real.cs"), "namespace Demo.Messages; public sealed record Real : IMessage; public interface IMessage { }");
        File.WriteAllText(Path.Combine(root, "Messages", "Other.cs"), "namespace Demo.Messages; public sealed record Other : IMessage;");
        File.WriteAllText(Path.Combine(root, "Messaging", "MessageTypeRegistry.cs"), "using Alias = Demo.Messages.Real; public static class MessageTypeRegistry { static MessageTypeRegistry() { Register(typeof(Alias), \"real\"); Register(typeof(Other), \"other\"); } static void Register(System.Type type, string key) { } }");
        File.WriteAllText(Path.Combine(root, "Services", "DemoService.cs"), "using Demo.Messages; namespace Demo.Services; public sealed class DemoService { public void Send() { var local = new Other(); EnqueueAsync(new Real(), null); EnqueueAsync(local, null); } private void EnqueueAsync(object message, object? transaction) { } }");
        File.WriteAllText(Path.Combine(root, "Services", "Handlers", "DemoHandler.cs"), "using Demo.Messages; namespace Demo.Services.Handlers; public sealed class DemoHandler : IMessageHandler<Real>, IIdAwareMessageHandler<Other> { } public interface IMessageHandler<T> { } public interface IIdAwareMessageHandler<T> { }");

        var modules = new ModuleResolver(new Dictionary<string, string> { ["Demo"] = "Services" });
        var actions = new[] { new CypherNode("Action", "Demo.Services.DemoService.Send", new Dictionary<string, object?>()) };
        var messages = new MessageParser().Parse(root, actions);
        var handlers = new MessageHandlerParser(modules).Parse(root, messages.Graph.Nodes);

        Assert.Equal(2, messages.Graph.Nodes.Count(node => node.Label == "Message"));
        Assert.Equal("real", Assert.Single(messages.Graph.Nodes, node => node.Id.EndsWith("Real", StringComparison.Ordinal)).Properties["key"]);
        Assert.Equal("other", Assert.Single(messages.Graph.Nodes, node => node.Id.EndsWith("Other", StringComparison.Ordinal)).Properties["key"]);
        Assert.Equal(2, messages.Graph.Edges.Count(edge => edge.Type == "PUBLISHES"));
        Assert.Single(handlers.Graph.Nodes, node => node.Properties["idAware"] is true);
        Assert.Equal(2, handlers.Graph.Edges.Count(edge => edge.Type == "HANDLED_BY"));
        Assert.Empty(messages.Warnings);
        Assert.Empty(handlers.Warnings);
        Directory.Delete(root, true);
    }

    [Fact]
    public void MessageHandlerParser_counts_a_multi_interface_handler_once()
    {
        var root = CreateMessageFixture(
            ("Messages/Catalog.cs", "namespace Demo.Messages; public sealed record One : IMessage; public sealed record Two : IMessage; public sealed record Three : IMessage; public sealed record Four : IMessage;"),
            ("Services/Handlers/CacheHandler.cs", "using Demo.Messages; namespace Demo.Services.Handlers; public sealed class CacheHandler : IMessageHandler<One>, IMessageHandler<Two>, IMessageHandler<Three>, IMessageHandler<Four> { }"));

        var messages = new MessageParser().Parse(root, []);
        var handlers = new MessageHandlerParser(MessageFixtureModules()).Parse(root, messages.Graph.Nodes);

        // The node count is the assertion that matters: four edges alone would also be produced by
        // a parser that emitted four duplicate nodes, which `Graph.MergeInto` would later collapse.
        Assert.Single(handlers.Graph.Nodes, node => node.Label == "MessageHandler");
        Assert.Equal(4, handlers.Graph.Edges.Count(edge => edge.Type == "HANDLED_BY"));
        Assert.Empty(handlers.Warnings);
        Directory.Delete(root, true);
    }

    [Fact]
    public void MessageParser_emits_no_module_contains_message_edge()
    {
        var root = CreateMessageFixture(
            ("Messages/Catalog.cs", "namespace Demo.Messages; public sealed record One : IMessage;"),
            ("Services/Handlers/CacheHandler.cs", "using Demo.Messages; namespace Demo.Services.Handlers; public sealed class CacheHandler : IMessageHandler<One> { }"));

        var messages = new MessageParser().Parse(root, []);
        var handlers = new MessageHandlerParser(MessageFixtureModules()).Parse(root, messages.Graph.Nodes);

        Assert.DoesNotContain(messages.Graph.Edges, edge => edge.Type == "CONTAINS");
        Assert.All(
            handlers.Graph.Edges.Where(edge => edge.Type == "CONTAINS"),
            edge => Assert.Equal("MessageHandler", edge.TargetLabel));
        Directory.Delete(root, true);
    }

    [Fact]
    public void MessageParser_keeps_unregistered_message_with_null_key_and_its_handler_edge()
    {
        var root = CreateMessageFixture(
            ("Messages/Orphan.cs", "namespace Demo.Messages; public sealed record Orphan : IMessage;"),
            ("Messaging/MessageTypeRegistry.cs", "public static class MessageTypeRegistry { static MessageTypeRegistry() { Register(typeof(Unrelated), \"unrelated\"); } static void Register(System.Type type, string key) { } }"),
            ("Services/Handlers/OrphanHandler.cs", "using Demo.Messages; namespace Demo.Services.Handlers; public sealed class OrphanHandler : IMessageHandler<Orphan> { }"));

        var messages = new MessageParser().Parse(root, []);
        var handlers = new MessageHandlerParser(MessageFixtureModules()).Parse(root, messages.Graph.Nodes);

        var message = Assert.Single(messages.Graph.Nodes, node => node.Label == "Message");
        Assert.Null(message.Properties["key"]);
        Assert.Contains(messages.Warnings, warning => warning.Contains("not registered in MessageTypeRegistry", StringComparison.Ordinal));
        Assert.Single(handlers.Graph.Edges, edge => edge.Type == "HANDLED_BY" && edge.SourceId == message.Id);
        Directory.Delete(root, true);
    }

    [Fact]
    public void MessageHandlerParser_warns_and_emits_no_edge_for_an_unknown_handled_type()
    {
        var root = CreateMessageFixture(
            ("Messages/Real.cs", "namespace Demo.Messages; public sealed record Real : IMessage;"),
            ("Services/Handlers/BadHandler.cs", "using Demo.Messages; namespace Demo.Services.Handlers; public sealed class BadHandler : IMessageHandler<NotAMessage> { }"));

        var messages = new MessageParser().Parse(root, []);
        var handlers = new MessageHandlerParser(MessageFixtureModules()).Parse(root, messages.Graph.Nodes);

        Assert.Single(handlers.Graph.Nodes, node => node.Label == "MessageHandler");
        Assert.DoesNotContain(handlers.Graph.Edges, edge => edge.Type == "HANDLED_BY");
        var warning = Assert.Single(handlers.Warnings);
        Assert.Contains("NotAMessage", warning, StringComparison.Ordinal);
        Directory.Delete(root, true);
    }

    /// <summary>
    /// Two messages share a simple name in different namespaces. Each publishing file's `using`
    /// directives decide which one it means — a global simple-name lookup would pick one winner and
    /// attach both publishes to it.
    /// </summary>
    [Fact]
    public void MessageParser_resolves_same_named_messages_by_the_publishing_file_usings()
    {
        var root = CreateMessageFixture(
            ("Fulfillment/Messages/RefundApproved.cs", "namespace Demo.Fulfillment.Messages; public sealed record RefundApproved : IMessage;"),
            ("Payments/Messages/RefundApproved.cs", "namespace Demo.Payments.Messages; public sealed record RefundApproved : IMessage;"),
            ("Fulfillment/Services/RefundService.cs", "using Demo.Fulfillment.Messages; namespace Demo.Fulfillment.Services; public sealed class RefundService { public void Approve() { _broker.EnqueueAsync(new RefundApproved(), null); } }"),
            ("Payments/Services/PaymentService.cs", "using Demo.Payments.Messages; namespace Demo.Payments.Services; public sealed class PaymentService { public void Confirm() { _broker.EnqueueAsync(new RefundApproved(), null); } }"));
        var actions = new[]
        {
            new CypherNode("Action", "Demo.Fulfillment.Services.RefundService.Approve", new Dictionary<string, object?>()),
            new CypherNode("Action", "Demo.Payments.Services.PaymentService.Confirm", new Dictionary<string, object?>())
        };

        var messages = new MessageParser().Parse(root, actions);

        Assert.Equal(2, messages.Graph.Nodes.Count(node => node.Label == "Message"));
        Assert.Contains(messages.Graph.Edges, edge =>
            edge.Type == "PUBLISHES" &&
            edge.SourceId == "Demo.Fulfillment.Services.RefundService.Approve" &&
            edge.TargetId == "Demo.Fulfillment.Messages.RefundApproved");
        Assert.Contains(messages.Graph.Edges, edge =>
            edge.Type == "PUBLISHES" &&
            edge.SourceId == "Demo.Payments.Services.PaymentService.Confirm" &&
            edge.TargetId == "Demo.Payments.Messages.RefundApproved");
        Directory.Delete(root, true);
    }

    /// <summary>
    /// Same collision, but the publishing file imports both namespaces and uses no alias. The name
    /// is genuinely ambiguous, so the only honest output is a warning and no edge. A resolver that
    /// guesses would still pass every other test in this file.
    /// </summary>
    [Fact]
    public void MessageParser_refuses_to_guess_when_both_namespaces_are_imported()
    {
        var root = CreateMessageFixture(
            ("Fulfillment/Messages/RefundApproved.cs", "namespace Demo.Fulfillment.Messages; public sealed record RefundApproved : IMessage;"),
            ("Payments/Messages/RefundApproved.cs", "namespace Demo.Payments.Messages; public sealed record RefundApproved : IMessage;"),
            ("Both/Services/BothService.cs", "using Demo.Fulfillment.Messages; using Demo.Payments.Messages; namespace Demo.Both.Services; public sealed class BothService { public void Go() { _broker.EnqueueAsync(new RefundApproved(), null); } }"));
        var actions = new[] { new CypherNode("Action", "Demo.Both.Services.BothService.Go", new Dictionary<string, object?>()) };

        var messages = new MessageParser().Parse(root, actions);

        Assert.DoesNotContain(messages.Graph.Edges, edge => edge.Type == "PUBLISHES");
        Assert.Contains(messages.Warnings, warning =>
            warning.Contains("RefundApproved", StringComparison.Ordinal) &&
            warning.Contains("Could not resolve message type", StringComparison.Ordinal));
        Directory.Delete(root, true);
    }

    /// <summary>
    /// The counter-test for "scan the whole method body for `new SomeMessage(...)`": a message that
    /// is constructed but never enqueued is not published.
    /// </summary>
    [Fact]
    public void MessageParser_ignores_a_message_constructed_but_never_enqueued()
    {
        var root = CreateMessageFixture(
            ("Messages/Ghost.cs", "namespace Demo.Messages; public sealed record Ghost : IMessage;"),
            ("Services/GhostService.cs", "using Demo.Messages; namespace Demo.Services; public sealed class GhostService { public void Go() { var ghost = new Ghost(); Log(ghost); } private void Log(object value) { } }"));
        var actions = new[] { new CypherNode("Action", "Demo.Services.GhostService.Go", new Dictionary<string, object?>()) };

        var messages = new MessageParser().Parse(root, actions);

        Assert.Single(messages.Graph.Nodes, node => node.Label == "Message");
        Assert.DoesNotContain(messages.Graph.Edges, edge => edge.Type == "PUBLISHES");
        Directory.Delete(root, true);
    }

    /// <summary>
    /// A publish from a handler has no `Action` to originate at, and the ontology has no
    /// `MessageHandler-[:PUBLISHES]->Message` triple. It produces no edge and — because the parser
    /// is doing exactly what the ontology allows — no warning either.
    /// </summary>
    [Fact]
    public void MessageParser_ignores_publishes_from_handler_files_without_warning()
    {
        var root = CreateMessageFixture(
            ("Messages/Alarm.cs", "namespace Demo.Messages; public sealed record Alarm : IMessage;"),
            ("Services/Handlers/AlarmHandler.cs", "using Demo.Messages; namespace Demo.Services.Handlers; public sealed class AlarmHandler : IMessageHandler<Alarm> { public void Handle() { _broker.EnqueueAsync(new Alarm(), null); } }"));

        var messages = new MessageParser().Parse(root, []);

        Assert.DoesNotContain(messages.Graph.Edges, edge => edge.Type == "PUBLISHES");
        Assert.DoesNotContain(messages.Warnings, warning => warning.Contains("Could not extract published message", StringComparison.Ordinal));
        Directory.Delete(root, true);
    }

    private static string CreateMessageFixture(params (string Path, string Content)[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), "kg-msg-" + Guid.NewGuid().ToString("N"));
        foreach (var (path, content) in files)
        {
            var full = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        return root;
    }

    private static ModuleResolver MessageFixtureModules() =>
        new(new Dictionary<string, string> { ["Demo"] = "Services" });

    /// <summary>
    /// The discriminating intersection test: neither role set contains the other, so union
    /// (`Administrator, Manager, Service`), "method wins" (`Manager, Service`) and "class wins"
    /// (`Administrator, Manager`) all produce a different answer from the intersection (`Manager`).
    /// The real `StockController` case has the method set nested inside the class set, which a
    /// "method wins" implementation would satisfy too — so it cannot stand in for this.
    /// </summary>
    [Fact]
    public void RolePolicyParser_intersects_rather_than_overriding_class_roles()
    {
        var root = CreateRoleFixture(
            "public const string ManagingRole = \"Administrator, Manager\"; public const string ServicingRole = \"Manager, Service\";",
            "[Authorize(Roles = ServicingRole)] public IActionResult Update() => Ok();",
            "[Authorize(Roles = ManagingRole)]");

        var result = ParseRoleFixture(root.Api, root.Web);

        Assert.Equal(["Manager"], result.Graph.Edges.Select(edge => edge.TargetId));
        Assert.Equal(["Manager"], result.Graph.Nodes.Where(node => node.Label == "Role").Select(node => node.Id));
        Assert.Empty(result.Warnings);
        Directory.Delete(root.Root, true);
    }

    [Fact]
    public void RolePolicyParser_warns_once_when_class_and_method_roles_share_no_role()
    {
        var root = CreateRoleFixture(
            "public const string AdminRole = \"Administrator\"; public const string ServiceRole = \"Service\";",
            "[Authorize(Roles = ServiceRole)] public IActionResult Update() => Ok();",
            "[Authorize(Roles = AdminRole)]");

        var result = ParseRoleFixture(root.Api, root.Web);

        Assert.Empty(result.Graph.Nodes);
        Assert.Empty(result.Graph.Edges);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("intersection is empty", warning, StringComparison.Ordinal);
        Directory.Delete(root.Root, true);
    }

    /// <summary>
    /// `UserPermissions.Roles.Administrator` names one atomic role reached through its declaring
    /// type. It must not go through the comma-splitting path that alias constants use.
    /// </summary>
    [Fact]
    public void RolePolicyParser_resolves_qualified_role_constant_as_a_single_role()
    {
        var root = CreateRoleFixture(
            "public const string MaintenanceRole = \"Administrator, Manager, Service\";",
            "[Authorize(Roles = UserPermissions.Roles.Administrator)] public IActionResult Get() => Ok();");

        var result = ParseRoleFixture(root.Api, root.Web);

        Assert.Equal(["Administrator"], result.Graph.Edges.Select(edge => edge.TargetId));
        Assert.Empty(result.Warnings);
        Directory.Delete(root.Root, true);
    }

    /// <summary>
    /// Renaming a role in `UserPermissions.Roles` must break loudly, not silently narrow the edge
    /// set: a literal naming a role the catalog does not declare is rejected whole.
    /// </summary>
    [Fact]
    public void RolePolicyParser_warns_for_literal_naming_an_undeclared_role()
    {
        var root = CreateRoleFixture(
            "public const string MaintenanceRole = \"Administrator\";",
            "[Authorize(Roles = \"Administrator, Auditor\")] public IActionResult Get() => Ok();");

        var result = ParseRoleFixture(root.Api, root.Web);

        Assert.Empty(result.Graph.Nodes);
        Assert.Empty(result.Graph.Edges);
        Assert.Contains(result.Warnings, warning => warning.Contains("Could not resolve roles", StringComparison.Ordinal));
        Directory.Delete(root.Root, true);
    }

    [Fact]
    public void QueryParser_uses_declared_namespace_when_folder_disagrees()
    {
        var root = Path.Combine(Path.GetTempPath(), "kg-queries-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Wrong", "Queries"));
        File.WriteAllText(Path.Combine(root, "Wrong", "Queries", "OrderExistsQuery.cs"),
            "namespace Demo.Contracts; public sealed record OrderExistsQuery : IQuery<bool>; public interface IQuery<T> { }");

        var result = new QueryParser().Parse(root, []);

        Assert.Equal("Demo.Contracts.OrderExistsQuery", Assert.Single(result.Graph.Nodes).Id);
        Directory.Delete(root, true);
    }

    [Fact]
    public void QueryParser_type_gate_rejects_non_module_client_service_field()
    {
        var root = Path.Combine(Path.GetTempPath(), "kg-query-gate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "EmailService.cs"), """
            namespace Demo;
            public interface IEmailSender { System.Threading.Tasks.Task SendAsync(object value); }
            public sealed class EmailService
            {
                private readonly IEmailSender _email;
                public EmailService(IEmailSender email) => _email = email;
                public void Send() => _email.SendAsync(new object());
            }
            """);
        var actions = new[] { new CypherNode("Action", "Demo.EmailService.Send", new Dictionary<string, object?>()) };

        var result = new QueryParser().Parse(root, actions);

        Assert.Empty(result.Graph.Edges);
        Assert.Empty(result.Warnings);
        Directory.Delete(root, true);
    }

    [Fact]
    public void QueryParser_skips_chained_send_async_receiver()
    {
        var root = Path.Combine(Path.GetTempPath(), "kg-query-chain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "NotifyService.cs"), """
            namespace Demo;
            public sealed class NotifyService
            {
                private readonly Hub _hub = new();
                public void Notify() => _hub.Clients.User("id").SendAsync(new object());
            }
            public sealed class Hub { public Clients Clients { get; } = new(); }
            public sealed class Clients { public User User(string id) => new(); }
            public sealed class User { public void SendAsync(object value) { } }
            """);
        var actions = new[] { new CypherNode("Action", "Demo.NotifyService.Notify", new Dictionary<string, object?>()) };

        var result = new QueryParser().Parse(root, actions);

        Assert.Empty(result.Graph.Edges);
        Assert.Empty(result.Warnings);
        Directory.Delete(root, true);
    }

    /// <summary>
    /// Two real occurrences name `IQuery<T>` without being a query: the `where TQuery : IQuery<TResult>`
    /// constraint on `IQueryHandler<,>` and the `IQuery<TResult>` parameter on `IModuleClient.SendAsync`.
    /// Matching on the base list rather than searching descendants is what keeps both out; a text or
    /// descendant search would emit two phantom `Query` nodes that nothing ever links to.
    /// </summary>
    [Fact]
    public void QueryParser_ignores_generic_constraint_and_parameter_occurrences()
    {
        var root = CreateQueryFixture(
            ("Messaging/IQuery.cs", "namespace Demo.Messaging; public interface IQuery<TResult> { }"),
            ("Messaging/IQueryHandler.cs", "namespace Demo.Messaging; public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult> { }"),
            ("Messaging/IModuleClient.cs", "namespace Demo.Messaging; public interface IModuleClient { System.Threading.Tasks.Task<TResult> SendAsync<TResult>(IQuery<TResult> query); }"),
            ("Orders/Queries/RealQuery.cs", "namespace Demo.Messaging; public sealed record RealQuery : IQuery<bool>;"));

        var result = new QueryParser().Parse(root, []);

        Assert.Equal("Demo.Messaging.RealQuery", Assert.Single(result.Graph.Nodes, node => node.Label == "Query").Id);
        Directory.Delete(root, true);
    }

    /// <summary>
    /// The cross-project pin: queries live in `ECommerceApp.Application`, their handlers in
    /// `ECommerceApp.Infrastructure`. A single-root fixture cannot exercise this — pointing the
    /// handler scan at the query root (the natural carry-over from Phase 4a, where both live under
    /// `Application`) yields zero handlers against the real repo.
    /// </summary>
    [Fact]
    public void QueryHandlerParser_resolves_handled_by_across_two_roots()
    {
        var (root, application, infrastructure) = CreateSplitQueryFixture(
            ("Inventory/Availability/Queries/StockAvailableQuery.cs", "namespace Demo.Messaging; public sealed record StockAvailableQuery : IQuery<bool>; public interface IQuery<T> { }"),
            ("Inventory/Handlers/StockAvailableQueryHandler.cs", "using Demo.Messaging; namespace Demo.Infrastructure.Inventory.Handlers; internal sealed class StockAvailableQueryHandler : IQueryHandler<StockAvailableQuery, bool> { } public interface IQueryHandler<TQuery, TResult> { }"));

        var queries = new QueryParser().Parse(application, []);
        var handlers = new QueryHandlerParser(QueryFixtureModules()).Parse(infrastructure, queries.Graph.Nodes);

        var handler = Assert.Single(handlers.Graph.Nodes, node => node.Label == "QueryHandler");
        Assert.Equal("Demo.Infrastructure.Inventory.Handlers.StockAvailableQueryHandler", handler.Id);
        Assert.Equal("bool", handler.Properties["resultType"]);
        var edge = Assert.Single(handlers.Graph.Edges, x => x.Type == "HANDLED_BY");
        Assert.Equal("Demo.Messaging.StockAvailableQuery", edge.SourceId);
        Assert.Equal(handler.Id, edge.TargetId);
        Assert.Empty(handlers.Warnings);
        Directory.Delete(root, true);
    }

    /// <summary>
    /// No real handler implements `IQueryHandler<,>` twice, so only a fixture can prove the parser
    /// collects every base-list match instead of taking the first. The node count is the load-bearing
    /// assertion: two duplicate nodes would also yield two edges, and `Graph.MergeInto` would later
    /// collapse them, hiding the defect at the graph level.
    /// </summary>
    [Fact]
    public void QueryHandlerParser_counts_a_double_implementing_handler_once()
    {
        var (root, application, infrastructure) = CreateSplitQueryFixture(
            ("Queries/Catalog.cs", "namespace Demo.Messaging; public sealed record One : IQuery<bool>; public sealed record Two : IQuery<int>; public interface IQuery<T> { }"),
            ("Inventory/Handlers/BothHandler.cs", "using Demo.Messaging; namespace Demo.Infrastructure.Inventory.Handlers; internal sealed class BothHandler : IQueryHandler<One, bool>, IQueryHandler<Two, int> { } public interface IQueryHandler<TQuery, TResult> { }"));

        var queries = new QueryParser().Parse(application, []);
        var handlers = new QueryHandlerParser(QueryFixtureModules()).Parse(infrastructure, queries.Graph.Nodes);

        Assert.Single(handlers.Graph.Nodes, node => node.Label == "QueryHandler");
        Assert.Equal(2, handlers.Graph.Edges.Count(edge => edge.Type == "HANDLED_BY"));
        Assert.Empty(handlers.Warnings);
        Directory.Delete(root, true);
    }

    /// <summary>
    /// Today's three queries all share one namespace and cannot collide, so this refusal is
    /// unreachable from the real repo. It is pinned anyway: a resolver that guesses on ambiguity is
    /// wrong regardless of whether current data happens to expose it.
    /// </summary>
    [Fact]
    public void QueryHandlerParser_refuses_to_guess_between_same_named_queries()
    {
        var (root, application, infrastructure) = CreateSplitQueryFixture(
            ("Orders/Queries/OrderExists.cs", "namespace Demo.Orders.Queries; public sealed record OrderExists : IQuery<bool>; public interface IQuery<T> { }"),
            ("Fulfillment/Queries/OrderExists.cs", "namespace Demo.Fulfillment.Queries; public sealed record OrderExists : IQuery<bool>;"),
            ("Inventory/Handlers/AmbiguousHandler.cs", "namespace Demo.Infrastructure.Inventory.Handlers; internal sealed class AmbiguousHandler : IQueryHandler<OrderExists, bool> { } public interface IQueryHandler<TQuery, TResult> { }"));

        var queries = new QueryParser().Parse(application, []);
        var handlers = new QueryHandlerParser(QueryFixtureModules()).Parse(infrastructure, queries.Graph.Nodes);

        Assert.Equal(2, queries.Graph.Nodes.Count(node => node.Label == "Query"));
        Assert.Single(handlers.Graph.Nodes, node => node.Label == "QueryHandler");
        Assert.DoesNotContain(handlers.Graph.Edges, edge => edge.Type == "HANDLED_BY");
        var warning = Assert.Single(handlers.Warnings);
        Assert.Contains("OrderExists", warning, StringComparison.Ordinal);
        Directory.Delete(root, true);
    }

    [Fact]
    public void QueryHandlerParser_skips_a_handler_whose_path_resolves_to_no_module()
    {
        var (root, application, infrastructure) = CreateSplitQueryFixture(
            ("Queries/Catalog.cs", "namespace Demo.Messaging; public sealed record One : IQuery<bool>; public interface IQuery<T> { }"),
            ("Unmapped/Handlers/OneQueryHandler.cs", "using Demo.Messaging; namespace Demo.Infrastructure.Unmapped.Handlers; internal sealed class OneQueryHandler : IQueryHandler<One, bool> { } public interface IQueryHandler<TQuery, TResult> { }"));

        var queries = new QueryParser().Parse(application, []);
        var handlers = new QueryHandlerParser(QueryFixtureModules()).Parse(infrastructure, queries.Graph.Nodes);

        Assert.Empty(handlers.Graph.Nodes);
        Assert.Empty(handlers.Graph.Edges);
        Assert.Empty(handlers.Warnings);
        Directory.Delete(root, true);
    }

    /// <summary>
    /// Handler selection reads the base list, not any `IQueryHandler` mention. The real DI
    /// registrations that would trip a descendant search live in `Extensions.cs` and `ModuleClient.cs`,
    /// which the `*Handler.cs` glob already excludes — so the gate is only provable in a fixture that
    /// puts the registration inside a `*Handler.cs` file.
    /// </summary>
    [Fact]
    public void QueryHandlerParser_ignores_a_di_registration_mention_of_the_marker()
    {
        var (root, application, infrastructure) = CreateSplitQueryFixture(
            ("Queries/Catalog.cs", "namespace Demo.Messaging; public sealed record One : IQuery<bool>; public interface IQuery<T> { }"),
            ("Inventory/Handlers/RegistrationHandler.cs", "using Demo.Messaging; namespace Demo.Infrastructure.Inventory.Handlers; internal static class RegistrationHandler { public static void Register(IServiceCollection services) { services.AddScoped<IQueryHandler<One, bool>, SomeHandler>(); } } public interface IQueryHandler<TQuery, TResult> { } public interface IServiceCollection { }"));

        var queries = new QueryParser().Parse(application, []);
        var handlers = new QueryHandlerParser(QueryFixtureModules()).Parse(infrastructure, queries.Graph.Nodes);

        Assert.Empty(handlers.Graph.Nodes);
        Assert.Empty(handlers.Graph.Edges);
        Directory.Delete(root, true);
    }

    /// <summary>
    /// No real send site uses a local today, so this path is fixture-only. Left untested it would
    /// fail silently — a missing `USES` edge produces no warning and no error.
    /// </summary>
    [Fact]
    public void QueryParser_emits_uses_for_a_query_held_in_a_local()
    {
        var root = CreateQueryFixture(
            ("Messaging/IModuleClient.cs", "namespace Demo.Messaging; public interface IModuleClient { }"),
            ("Orders/Queries/OrderExistsQuery.cs", "namespace Demo.Messaging; public sealed record OrderExistsQuery : IQuery<bool>; public interface IQuery<T> { }"),
            ("Orders/Services/OrderService.cs", "using Demo.Messaging; namespace Demo.Orders.Services; public sealed class OrderService { private readonly IModuleClient _client; public OrderService(IModuleClient client) => _client = client; public void Check() { var query = new OrderExistsQuery(); _client.SendAsync(query, default); } }"));
        var actions = new[] { new CypherNode("Action", "Demo.Orders.Services.OrderService.Check", new Dictionary<string, object?>()) };

        var result = new QueryParser().Parse(root, actions);

        var edge = Assert.Single(result.Graph.Edges, x => x.Type == "USES");
        Assert.Equal("Demo.Orders.Services.OrderService.Check", edge.SourceId);
        Assert.Equal("Demo.Messaging.OrderExistsQuery", edge.TargetId);
        Assert.Empty(result.Warnings);
        Directory.Delete(root, true);
    }

    /// <summary>
    /// `Query` is a cross-context contract owned by no module, and `Module-[:CONTAINS]->Query` is not
    /// a declared triple — emitting one would fail `GraphValidator`. Its handler is contained; the
    /// query is not.
    /// </summary>
    [Fact]
    public void Query_parsers_emit_no_module_contains_query_edge()
    {
        var (root, application, infrastructure) = CreateSplitQueryFixture(
            ("Queries/Catalog.cs", "namespace Demo.Messaging; public sealed record One : IQuery<bool>; public interface IQuery<T> { }"),
            ("Inventory/Handlers/OneQueryHandler.cs", "using Demo.Messaging; namespace Demo.Infrastructure.Inventory.Handlers; internal sealed class OneQueryHandler : IQueryHandler<One, bool> { } public interface IQueryHandler<TQuery, TResult> { }"));

        var queries = new QueryParser().Parse(application, []);
        var handlers = new QueryHandlerParser(QueryFixtureModules()).Parse(infrastructure, queries.Graph.Nodes);

        Assert.DoesNotContain(queries.Graph.Edges, edge => edge.Type == "CONTAINS");
        Assert.All(
            handlers.Graph.Edges.Where(edge => edge.Type == "CONTAINS"),
            edge => Assert.Equal("QueryHandler", edge.TargetLabel));
        Directory.Delete(root, true);
    }

    [Fact]
    public void ScriptModuleParser_does_not_treat_require_as_a_module_declaration()
    {
        var root = CreateScriptModuleFixture(("wwwroot/js/config.js", "require(['known'], function (known) { });"));

        var result = ParseScriptModuleFixture(root);

        Assert.Empty(result.Graph.Nodes);
        Assert.Empty(result.Warnings);
        Directory.Delete(root, true);
    }

    [Fact]
    public void ScriptModuleParser_accepts_empty_dependency_array_without_warning()
    {
        var root = CreateScriptModuleFixture(("wwwroot/js/common.js", "define([], function () { });"));

        var result = ParseScriptModuleFixture(root);

        Assert.Single(result.Graph.Nodes, node => node.Id == "common");
        Assert.DoesNotContain(result.Graph.Edges, edge => edge.Type == "DEPENDS_ON");
        Assert.Empty(result.Warnings);
        Directory.Delete(root, true);
    }

    [Fact]
    public void ScriptModuleParser_warns_without_fabricating_unknown_dependency()
    {
        var root = CreateScriptModuleFixture(("wwwroot/js/common.js", "define(['nope'], function (nope) { });"));

        var result = ParseScriptModuleFixture(root);

        Assert.Single(result.Graph.Nodes, node => node.Id == "common");
        Assert.DoesNotContain(result.Graph.Nodes, node => node.Id == "nope");
        Assert.DoesNotContain(result.Graph.Edges, edge => edge.Type == "DEPENDS_ON");
        Assert.Contains(result.Warnings, warning => warning.Contains("unknown module 'nope'", StringComparison.Ordinal));
        Directory.Delete(root, true);
    }

    [Fact]
    public void ScriptModuleParser_attaches_view_usage_to_all_overload_pages()
    {
        var root = CreateScriptModuleFixture(
            ("wwwroot/js/checkout-placeorder.js", "define([], function () { });"),
            ("Areas/Presale/Views/Checkout/PlaceOrder.cshtml", "require(['checkout-placeorder'], function () { });"));
        var pages = new[]
        {
            new CypherNode("Page", "Demo.Web.Areas.Presale.Controllers.CheckoutController.PlaceOrder", new Dictionary<string, object?>()),
            new CypherNode("Page", "Demo.Web.Areas.Presale.Controllers.CheckoutController.PlaceOrder#2", new Dictionary<string, object?>())
        };

        var result = ParseScriptModuleFixture(root, pages);

        Assert.Equal(2, result.Graph.Edges.Count(edge => edge.Type == "USES"));
        Assert.All(result.Graph.Edges.Where(edge => edge.Type == "USES"), edge =>
        {
            Assert.Equal("checkout-placeorder", edge.TargetId);
            Assert.Contains(edge.SourceId, pages.Select(page => page.Id));
        });
        Directory.Delete(root, true);
    }

    [Fact]
    public void ScriptModuleParser_ignores_layout_usage_structurally()
    {
        var root = CreateScriptModuleFixture(
            ("wwwroot/js/errors.js", "define([], function () { });"),
            ("Views/Shared/_Layout.cshtml", "require(['errors'], function () { });"));

        var result = ParseScriptModuleFixture(root);

        Assert.DoesNotContain(result.Graph.Edges, edge => edge.Type == "USES");
        Assert.Empty(result.Warnings);
        Directory.Delete(root, true);
    }

    [Fact]
    public void ScriptModuleParser_matches_view_area_before_controller_and_method()
    {
        var root = CreateScriptModuleFixture(
            ("wwwroot/js/orders.js", "define([], function () { });"),
            ("Areas/A/Views/Orders/Index.cshtml", "require(['orders'], function () { });"));
        var pages = new[]
        {
            new CypherNode("Page", "Demo.Web.Areas.B.Controllers.OrdersController.Index", new Dictionary<string, object?>()),
            new CypherNode("Page", "Demo.Web.Areas.A.Controllers.OrdersController.Index", new Dictionary<string, object?>())
        };

        var result = ParseScriptModuleFixture(root, pages);

        Assert.Single(result.Graph.Edges, edge => edge.Type == "USES");
        Assert.Equal("Demo.Web.Areas.A.Controllers.OrdersController.Index", result.Graph.Edges.Single(edge => edge.Type == "USES").SourceId);
        Directory.Delete(root, true);
    }

    [Fact]
    public void ScriptModuleParser_silences_unresolved_same_host_fetch()
    {
        var root = CreateScriptModuleFixture(("Views/Orders/Index.cshtml", "fetch('/Area/Controller/Action', { });"));
        var pages = new[]
        {
            new CypherNode("Page", "Demo.Web.Controllers.OrdersController.Index", new Dictionary<string, object?>())
        };

        var result = ParseScriptModuleFixture(root, pages);

        Assert.DoesNotContain(result.Graph.Edges, edge => edge.Type == "USES");
        Assert.Empty(result.Warnings);
        Directory.Delete(root, true);
    }

    [Fact]
    public void ScriptModuleParser_warns_for_unresolved_api_fetch()
    {
        var root = CreateScriptModuleFixture(("Views/Orders/Index.cshtml", "fetch('/api/whatever', { });"));
        var pages = new[]
        {
            new CypherNode("Page", "Demo.Web.Controllers.OrdersController.Index", new Dictionary<string, object?>())
        };

        var result = ParseScriptModuleFixture(root, pages);

        Assert.Contains(result.Warnings, warning => warning.Contains("/api/whatever", StringComparison.Ordinal));
        Directory.Delete(root, true);
    }

    /// <summary>
    /// The real specimens are `Areas/Catalog/Views/Product/{Add,Edit}ItemNew.cshtml`: view files
    /// rendered by `Create`/`Edit` through `return View("AddItemNew", …)`, so no `Page` id can ever
    /// carry their filename. They contain only same-host MVC `fetch(...)` calls, which resolve to no
    /// `Endpoint` — nothing was left unresolved, so nothing may be reported.
    /// </summary>
    [Fact]
    public void ScriptModuleParser_stays_silent_when_an_unmatched_view_has_no_attachable_usage()
    {
        var root = CreateScriptModuleFixture(
            ("Areas/Catalog/Views/Product/AddItemNew.cshtml", "fetch('/Catalog/Image/InitUpload', { });"));
        var pages = new[]
        {
            new CypherNode("Page", "Demo.Web.Areas.Catalog.Controllers.ProductController.Create", new Dictionary<string, object?>())
        };

        var result = ParseScriptModuleFixture(root, pages);

        Assert.DoesNotContain(result.Graph.Edges, edge => edge.Type == "USES");
        Assert.Empty(result.Warnings);
        Directory.Delete(root, true);
    }

    [Fact]
    public void ScriptModuleParser_warns_when_an_unmatched_view_blocks_a_real_module_edge()
    {
        var root = CreateScriptModuleFixture(
            ("wwwroot/js/checkout-placeorder.js", "define([], function () { });"),
            ("Areas/Presale/Views/Checkout/Orphan.cshtml", "require(['checkout-placeorder'], function () { });"));

        var result = ParseScriptModuleFixture(root);

        Assert.DoesNotContain(result.Graph.Edges, edge => edge.Type == "USES");
        Assert.Contains(result.Warnings, warning => warning.Contains("Orphan.cshtml", StringComparison.Ordinal) && warning.Contains("to a Page node", StringComparison.Ordinal));
        Directory.Delete(root, true);
    }

    /// <summary>
    /// Razor Pages and any other non-`Views/{Controller}/{Method}.cshtml` shape yield no view shape at
    /// all. Same rule: report only when an edge was genuinely blocked.
    /// </summary>
    [Fact]
    public void ScriptModuleParser_reports_an_unmappable_view_shape_only_when_it_blocks_an_edge()
    {
        var silent = CreateScriptModuleFixture(
            ("Areas/Identity/Pages/Account/Manage.cshtml", "fetch('/Identity/Account/Profile', { });"));
        var blocked = CreateScriptModuleFixture(
            ("wwwroot/js/errors.js", "define([], function () { });"),
            ("Areas/Identity/Pages/Account/Manage.cshtml", "require(['errors'], function () { });"));

        var silentResult = ParseScriptModuleFixture(silent);
        var blockedResult = ParseScriptModuleFixture(blocked);

        Assert.Empty(silentResult.Warnings);
        Assert.Contains(blockedResult.Warnings, warning => warning.Contains("Could not map Razor view", StringComparison.Ordinal));
        Directory.Delete(silent, true);
        Directory.Delete(blocked, true);
    }

    private static string CreateQueryFixture(params (string Path, string Content)[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), "kg-query-" + Guid.NewGuid().ToString("N"));
        WriteFixtureFiles(root, files);
        return root;
    }

    /// <summary>
    /// Two sibling roots under one temp directory, mirroring the real `ECommerceApp.Application` /
    /// `ECommerceApp.Infrastructure` split. Files are routed by their leading `Queries`/`Orders`/
    /// `Fulfillment` segment being an application concern and `Inventory`/`Unmapped` handler folders
    /// being an infrastructure one — the two parsers must be handed genuinely different roots.
    /// </summary>
    private static (string Root, string Application, string Infrastructure) CreateSplitQueryFixture(
        params (string Path, string Content)[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), "kg-query-split-" + Guid.NewGuid().ToString("N"));
        var application = Path.Combine(root, "application");
        var infrastructure = Path.Combine(root, "infrastructure");
        foreach (var (path, content) in files)
        {
            var target = path.Contains("/Handlers/", StringComparison.Ordinal) ? infrastructure : application;
            WriteFixtureFiles(target, [(path, content)]);
        }

        Directory.CreateDirectory(application);
        Directory.CreateDirectory(infrastructure);
        return (root, application, infrastructure);
    }

    private static void WriteFixtureFiles(string root, (string Path, string Content)[] files)
    {
        foreach (var (path, content) in files)
        {
            var full = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }
    }

    private static ModuleResolver QueryFixtureModules() =>
        new(new Dictionary<string, string> { ["Inventory"] = "Inventory" });

    private static string CreateScriptModuleFixture(params (string Path, string Content)[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), "kg-script-modules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "wwwroot", "js"));
        WriteFixtureFiles(root, files);
        return root;
    }

    private static ParserResult ParseScriptModuleFixture(
        string root,
        IReadOnlyList<CypherNode>? pages = null,
        IReadOnlyList<CypherNode>? endpoints = null) =>
        new ScriptModuleParser().Parse(root, pages ?? [], endpoints ?? []);

    private static (string Root, string Api, string Web) CreateRoleFixture(string alias, string methods, string classAttributes = "")
    {
        var root = Path.Combine(Path.GetTempPath(), "kg-role-" + Guid.NewGuid().ToString("N"));
        var api = Path.Combine(root, "api");
        var web = Path.Combine(root, "web");
        Directory.CreateDirectory(Path.Combine(api, "Controllers"));
        Directory.CreateDirectory(Path.Combine(web, "Controllers"));
        var application = Path.Combine(root, "application");
        Directory.CreateDirectory(application);
        File.WriteAllText(Path.Combine(application, "UserPermissions.cs"), "public static class UserPermissions { public static class Roles { public const string Administrator = \"Administrator\"; public const string Manager = \"Manager\"; public const string Service = \"Service\"; } }");
        File.WriteAllText(Path.Combine(api, "Controllers", "BaseController.cs"), $"public class BaseController : Microsoft.AspNetCore.Mvc.ControllerBase {{ {alias} }}");
        File.WriteAllText(Path.Combine(api, "Controllers", "OrdersController.cs"), $"using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; [ApiController] {classAttributes} public class OrdersController : BaseController {{ {methods} }}");
        return (root, api, web);
    }

    private static ParserResult ParseRoleFixture(string api, string web, string? application = null)
    {
        application ??= Path.Combine(Directory.GetParent(api)!.FullName, "application");
        var symbols = DomainSymbolIndex.Build(application);
        var endpoint = new EndpointParser().Parse(api, symbols, []);
        var page = new PageParser().Parse(web, symbols, []);
        return new RolePolicyParser().Parse(application, api, web, endpoint.Graph.Nodes, page.Graph.Nodes);
    }

    /// <summary>
    /// `CurrencyRateSyncTask` really declares `TaskName => "CurrencyDownloader"`, and the three
    /// `*CleanupTask` classes drop their suffix. A parser that derives the task name from the class
    /// name gets four of the nine real jobs wrong while still emitting nine plausible nodes.
    /// </summary>
    [Fact]
    public void JobParser_reads_task_name_from_a_literal_unrelated_to_the_class_name()
    {
        var root = CreateJobFixture(("Jobs/CurrencyRateSyncTask.cs", """
            namespace Demo.Jobs;
            public interface IScheduledTask { }
            internal sealed class CurrencyRateSyncTask : IScheduledTask
            {
                public string TaskName => "CurrencyDownloader";
            }
            """));

        var result = ParseJobFixture(root);

        var job = Assert.Single(result.Graph.Nodes, node => node.Label == "Job");
        Assert.Equal("Demo.Jobs.CurrencyRateSyncTask", job.Id);
        Assert.Equal("CurrencyDownloader", job.Properties["taskName"]);
        Assert.Single(result.Graph.Edges, edge => edge.Type == "CONTAINS" && edge.SourceId == "Demo" && edge.TargetId == job.Id);
        Directory.Delete(root, true);
    }

    /// <summary>
    /// `SnapshotOrderItemsJob` declares `private const int BatchSize = 64;` before its
    /// `JobTaskName` const. "The first const in the class" would yield `64`; the identifier the
    /// `TaskName` getter actually names is the only correct source.
    /// </summary>
    [Fact]
    public void JobParser_resolves_the_backing_const_by_name_not_by_declaration_order()
    {
        var root = CreateJobFixture(("Jobs/SnapshotOrderItemsJob.cs", """
            namespace Demo.Jobs;
            public interface IScheduledTask { }
            public sealed class SnapshotOrderItemsJob : IScheduledTask
            {
                private const int BatchSize = 64;
                public const string JobTaskName = "SnapshotOrderItemsJob";
                public string TaskName => JobTaskName;
            }
            """));

        var result = ParseJobFixture(root);

        Assert.Equal("SnapshotOrderItemsJob", Assert.Single(result.Graph.Nodes, node => node.Label == "Job").Properties["taskName"]);
        Directory.Delete(root, true);
    }

    /// <summary>
    /// The highest-value test in Phase 4c. `IDeferredJobScheduler` declares `ScheduleAsync` and
    /// `CancelAsync` with an identical first-argument shape, and the real repo has 8 `CancelAsync`
    /// call sites against 4 `ScheduleAsync` ones. A parser gated only on the field's declared type
    /// emits 10 plausible `SCHEDULES` edges instead of 4.
    /// The fixture carries **both** real shapes, because only the second one is detectable:
    /// `Adjust` mirrors `StockService.AdjustAsync`, which cancels and schedules the *same* job on
    /// adjacent lines — edge de-duplication hides a missing gate there entirely. `Release` mirrors
    /// `OrderPlacementFailedHandler`, which only ever cancels; that is the site where an ungated
    /// parser produces a second, distinct, entirely wrong edge.
    /// </summary>
    [Fact]
    public void JobParser_ignores_cancel_async_on_a_field_of_the_scheduler_type()
    {
        var root = CreateJobFixture(
            ("Jobs/ExpiryJob.cs", JobClass("Demo.Jobs", "ExpiryJob", "Expiry")),
            ("Jobs/HoldJob.cs", JobClass("Demo.Jobs", "HoldJob", "Hold")),
            ("Services/StockService.cs", """
                using Demo.Jobs;
                namespace Demo.Services;
                public interface IDeferredJobScheduler { }
                public sealed class StockService
                {
                    private readonly IDeferredJobScheduler _scheduler;
                    public StockService(IDeferredJobScheduler scheduler) => _scheduler = scheduler;
                    public void Adjust()
                    {
                        _scheduler.CancelAsync(ExpiryJob.JobTaskName, "1");
                        _scheduler.ScheduleAsync(ExpiryJob.JobTaskName, "1", default);
                    }
                    public void Release() => _scheduler.CancelAsync(HoldJob.JobTaskName, "1");
                }
                """));
        var actions = new[]
        {
            new CypherNode("Action", "Demo.Services.StockService.Adjust", new Dictionary<string, object?>()),
            new CypherNode("Action", "Demo.Services.StockService.Release", new Dictionary<string, object?>())
        };

        var result = ParseJobFixture(root, actions: actions);

        Assert.Equal(1, result.Graph.Edges.Count(edge => edge.Type == "SCHEDULES"));
        var edge = Assert.Single(result.Graph.Edges, x => x.Type == "SCHEDULES");
        Assert.Equal("Action", edge.SourceLabel);
        Assert.Equal("Demo.Services.StockService.Adjust", edge.SourceId);
        Assert.Equal("Demo.Jobs.ExpiryJob", edge.TargetId);
        Assert.Equal("Deferred", Assert.Single(result.Graph.Nodes, node => node.Id == "Demo.Jobs.ExpiryJob").Properties["triggerMode"]);
        Assert.Null(Assert.Single(result.Graph.Nodes, node => node.Id == "Demo.Jobs.HoldJob").Properties["triggerMode"]);
        Directory.Delete(root, true);
    }

    /// <summary>
    /// The fourth real `ScheduleAsync` site is in `OrderPlacedHandler`, not a service — so the scan
    /// cannot reuse `ActionParser`'s `*Service.cs` glob, and the edge is `MessageHandler`-sourced.
    /// </summary>
    [Fact]
    public void JobParser_emits_a_message_handler_sourced_schedules_edge()
    {
        var root = CreateJobFixture(
            ("Jobs/ExpiryJob.cs", JobClass("Demo.Jobs", "ExpiryJob", "Expiry")),
            ("Handlers/OrderPlacedHandler.cs", """
                using Demo.Jobs;
                namespace Demo.Sales.Handlers;
                public interface IDeferredJobScheduler { }
                public sealed class OrderPlacedHandler
                {
                    private readonly IDeferredJobScheduler _deferredScheduler;
                    public OrderPlacedHandler(IDeferredJobScheduler scheduler) => _deferredScheduler = scheduler;
                    public void HandleAsync()
                    {
                        _deferredScheduler.ScheduleAsync(
                            ExpiryJob.JobTaskName,
                            "1",
                            default);
                    }
                }
                """));
        var handlers = new[] { new CypherNode("MessageHandler", "Demo.Sales.Handlers.OrderPlacedHandler", new Dictionary<string, object?>()) };

        var result = ParseJobFixture(root, messageHandlers: handlers);

        var edge = Assert.Single(result.Graph.Edges, x => x.Type == "SCHEDULES");
        Assert.Equal("MessageHandler", edge.SourceLabel);
        Assert.Equal("Demo.Sales.Handlers.OrderPlacedHandler", edge.SourceId);
        Directory.Delete(root, true);
    }

    /// <summary>
    /// Three distinct `OrderPlacedHandler` classes exist in the real repo (Inventory, Presale, Sales)
    /// and only the Sales one schedules. A simple-class-name lookup for the edge's *source* is
    /// genuinely ambiguous, so the source id must come from the enclosing type's own namespace
    /// declaration. Only the target is resolved by simple name, against the job index.
    /// </summary>
    [Fact]
    public void JobParser_sources_the_edge_from_the_scheduling_namespace_not_the_class_name()
    {
        var root = CreateJobFixture(
            ("Jobs/ExpiryJob.cs", JobClass("Demo.Jobs", "ExpiryJob", "Expiry")),
            ("Inventory/OrderPlacedHandler.cs", """
                namespace Demo.Inventory.Handlers;
                public sealed class OrderPlacedHandler
                {
                    public void HandleAsync() { }
                }
                """),
            ("Sales/OrderPlacedHandler.cs", """
                using Demo.Jobs;
                namespace Demo.Sales.Handlers;
                public interface IDeferredJobScheduler { }
                public sealed class OrderPlacedHandler
                {
                    private readonly IDeferredJobScheduler _scheduler;
                    public OrderPlacedHandler(IDeferredJobScheduler scheduler) => _scheduler = scheduler;
                    public void HandleAsync() => _scheduler.ScheduleAsync(ExpiryJob.JobTaskName, "1", default);
                }
                """));
        var handlers = new[]
        {
            new CypherNode("MessageHandler", "Demo.Inventory.Handlers.OrderPlacedHandler", new Dictionary<string, object?>()),
            new CypherNode("MessageHandler", "Demo.Sales.Handlers.OrderPlacedHandler", new Dictionary<string, object?>())
        };

        var result = ParseJobFixture(root, messageHandlers: handlers);

        var edge = Assert.Single(result.Graph.Edges, x => x.Type == "SCHEDULES");
        Assert.Equal("Demo.Sales.Handlers.OrderPlacedHandler", edge.SourceId);
        Assert.DoesNotContain(result.Graph.Edges, x => x.SourceId == "Demo.Inventory.Handlers.OrderPlacedHandler");
        Directory.Delete(root, true);
    }

    /// <summary>
    /// First of the two-kinds-of-empty pair. `CurrencyRateSyncTask` injects only
    /// `ICurrencyRateService` — nothing was left unresolved, the job simply touches no repository, so
    /// the correct output is zero edges and **no** repository warning. (The trigger-mode warning is a
    /// separate, deliberate one; see the `triggerMode` test.)
    /// </summary>
    [Fact]
    public void JobParser_is_silent_for_a_job_with_no_repository_field()
    {
        var root = CreateJobFixture(("Jobs/CurrencyRateSyncTask.cs", """
            namespace Demo.Jobs;
            public interface IScheduledTask { }
            public interface ICurrencyRateService { }
            public sealed class CurrencyRateSyncTask : IScheduledTask
            {
                private readonly ICurrencyRateService _rates;
                public CurrencyRateSyncTask(ICurrencyRateService rates) => _rates = rates;
                public string TaskName => "CurrencyDownloader";
            }
            """));

        var result = ParseJobFixture(root);

        Assert.DoesNotContain(result.Graph.Edges, edge => edge.Type == "OPERATES_ON");
        Assert.DoesNotContain(result.Warnings, warning => warning.Contains("repository interface", StringComparison.Ordinal));
        Directory.Delete(root, true);
    }

    /// <summary>
    /// Second of the pair. `IInboxCleanupRepository` and `IOutboxRepository` are declared under
    /// `ECommerceApp.Application/Messaging/`, which `RepositoryParser` never scans — so no
    /// `Repository` node exists for them at all. That is a real modelling gap and must be named in a
    /// warning, not silently indistinguishable from the no-field case above.
    /// </summary>
    [Fact]
    public void JobParser_warns_once_for_a_repository_field_with_no_matching_node()
    {
        var root = CreateJobFixture(("Jobs/InboxCleanupTask.cs", """
            namespace Demo.Jobs;
            public interface IScheduledTask { }
            public interface IInboxCleanupRepository { }
            public sealed class InboxCleanupTask : IScheduledTask
            {
                private readonly IInboxCleanupRepository _inbox;
                public InboxCleanupTask(IInboxCleanupRepository inbox) => _inbox = inbox;
                public string TaskName => "InboxCleanup";
            }
            """));

        var result = ParseJobFixture(root);

        Assert.DoesNotContain(result.Graph.Edges, edge => edge.Type == "OPERATES_ON");
        var warning = Assert.Single(result.Warnings, item => item.Contains("repository interface", StringComparison.Ordinal));
        Assert.Contains("IInboxCleanupRepository", warning, StringComparison.Ordinal);
        Directory.Delete(root, true);
    }

    /// <summary>
    /// The positive half: an `OPERATES_ON` target is reached by walking a real
    /// `Entity-[:PERSISTED_BY]->Repository` edge backwards, so the edge lands on the `Entity` and
    /// never on the `Repository` the job actually injects.
    /// </summary>
    [Fact]
    public void JobParser_walks_persisted_by_backwards_to_reach_the_entity()
    {
        var root = CreateJobFixture(("Jobs/StockAdjustmentJob.cs", """
            namespace Demo.Jobs;
            public interface IScheduledTask { }
            public interface IStockItemRepository { }
            public sealed class StockAdjustmentJob : IScheduledTask
            {
                private readonly IStockItemRepository _stock;
                public StockAdjustmentJob(IStockItemRepository stock) => _stock = stock;
                public string TaskName => "StockAdjustmentJob";
            }
            """));
        var repositories = new[] { new CypherNode("Repository", "Demo.Domain.IStockItemRepository", new Dictionary<string, object?>()) };
        var persistedBy = new[] { new CypherEdge("PERSISTED_BY", "Entity", "Demo.Domain.StockItem", "Repository", "Demo.Domain.IStockItemRepository") };

        var result = ParseJobFixture(root, repositories: repositories, persistedByEdges: persistedBy);

        var edge = Assert.Single(result.Graph.Edges, x => x.Type == "OPERATES_ON");
        Assert.Equal("Job", edge.SourceLabel);
        Assert.Equal("Demo.Jobs.StockAdjustmentJob", edge.SourceId);
        Assert.Equal("Entity", edge.TargetLabel);
        Assert.Equal("Demo.Domain.StockItem", edge.TargetId);
        Assert.DoesNotContain(result.Warnings, warning => warning.Contains("repository interface", StringComparison.Ordinal));
        Directory.Delete(root, true);
    }

    /// <summary>
    /// `JobTriggerSource.Scheduled` and `.Manual` are properties of rows in the runtime `ScheduledJob`
    /// table, invisible to a syntax parser. Only `Deferred` has a findable call site, so a job without
    /// one gets `null` plus a warning — never a guessed mode presented as a fact.
    /// </summary>
    [Fact]
    public void JobParser_leaves_trigger_mode_null_and_warns_rather_than_guessing()
    {
        var root = CreateJobFixture(("Jobs/RefreshTokenCleanupTask.cs", """
            namespace Demo.Jobs;
            public interface IScheduledTask { }
            public sealed class RefreshTokenCleanupTask : IScheduledTask
            {
                public string TaskName => "RefreshTokenCleanup";
            }
            """));

        var result = ParseJobFixture(root);

        var job = Assert.Single(result.Graph.Nodes, node => node.Label == "Job");
        Assert.Null(job.Properties["triggerMode"]);
        var warning = Assert.Single(result.Warnings, item => item.Contains("trigger mode", StringComparison.Ordinal));
        Assert.Contains("Demo.Jobs.RefreshTokenCleanupTask", warning, StringComparison.Ordinal);
        Assert.DoesNotContain("unscheduled", warning, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Graph.Nodes, node =>
            Equals(node.Properties.GetValueOrDefault("triggerMode"), "Scheduled") ||
            Equals(node.Properties.GetValueOrDefault("triggerMode"), "Manual"));
        Directory.Delete(root, true);
    }

    /// <summary>
    /// Emit-only-if-it-exists: an enqueued type that resolves to no `Message` node produces no edge.
    /// A fabricated target would validate against the ontology and look correct in a graph browser.
    /// </summary>
    [Fact]
    public void JobParser_emits_no_publish_edge_for_a_message_with_no_node()
    {
        var root = CreateJobFixture(("Jobs/GhostJob.cs", """
            namespace Demo.Jobs;
            public interface IScheduledTask { }
            public interface IOutboxWriter { }
            public sealed record Ghost;
            public sealed class GhostJob : IScheduledTask
            {
                private readonly IOutboxWriter _outboxWriter;
                public GhostJob(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;
                public string TaskName => "Ghost";
                public void ExecuteAsync() => _outboxWriter.EnqueueAsync(new Ghost(), null, default);
            }
            """));

        var result = ParseJobFixture(root);

        Assert.DoesNotContain(result.Graph.Edges, edge => edge.Type == "PUBLISHES");
        Assert.Contains(result.Warnings, warning =>
            warning.Contains("Could not resolve message type", StringComparison.Ordinal) &&
            warning.Contains("Ghost", StringComparison.Ordinal));
        Directory.Delete(root, true);
    }

    private static string JobClass(string namespaceName, string className, string taskName) => $$"""
        namespace {{namespaceName}};
        public interface IScheduledTask { }
        public sealed class {{className}} : IScheduledTask
        {
            public const string JobTaskName = "{{taskName}}";
            public string TaskName => JobTaskName;
        }
        """;

    private static string CreateJobFixture(params (string Path, string Content)[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), "kg-jobs-" + Guid.NewGuid().ToString("N"));
        WriteFixtureFiles(root, files);
        return root;
    }

    private static ParserResult ParseJobFixture(
        string root,
        IReadOnlyList<CypherNode>? actions = null,
        IReadOnlyList<CypherNode>? messageHandlers = null,
        IReadOnlyList<CypherNode>? messages = null,
        IReadOnlyList<CypherNode>? repositories = null,
        IReadOnlyList<CypherEdge>? persistedByEdges = null)
    {
        return new JobParser(JobFixtureModules()).Parse(
            root,
            actions ?? [],
            messageHandlers ?? [],
            messages ?? [],
            repositories ?? [],
            persistedByEdges ?? []);
    }

    private static ModuleResolver JobFixtureModules() =>
        new(new Dictionary<string, string> { ["Demo"] = "Jobs" });
}
