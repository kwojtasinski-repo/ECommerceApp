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
}