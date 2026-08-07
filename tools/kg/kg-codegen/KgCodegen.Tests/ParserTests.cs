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
}
