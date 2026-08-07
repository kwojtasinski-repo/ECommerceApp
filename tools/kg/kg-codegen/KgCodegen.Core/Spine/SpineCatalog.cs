using KgCodegen.Core.Model;

namespace KgCodegen.Core.Spine;

public static class SpineCatalog
{
    private static readonly (string Id, string Path)[] Modules =
    [
        ("AccountProfile", "AccountProfile"), ("Backoffice", "Backoffice"), ("Catalog", "Catalog"),
        ("IAM", "Identity/IAM"), ("Inventory", "Inventory"), ("Checkout", "Presale/Checkout"),
        ("Orders", "Sales/Orders"), ("Payments", "Sales/Payments"), ("Coupons", "Sales/Coupons"),
        ("Fulfillment", "Sales/Fulfillment"), ("Communication", "Supporting/Communication"),
        ("Currencies", "Supporting/Currencies"), ("TimeManagement", "Supporting/TimeManagement"),
        ("Messaging", "Messaging")
    ];

    public static IReadOnlyDictionary<string, string> Paths => Modules.ToDictionary(x => x.Id, x => x.Path);

    public static Graph Create()
    {
        var graph = Graph.Empty();
        graph.Nodes.Add(new CypherNode("System", "ECommerceApp", new Dictionary<string, object?>()));
        foreach (var module in Modules)
        {
            graph.Nodes.Add(new CypherNode("Module", module.Id, new Dictionary<string, object?> { ["path"] = module.Path }));
            graph.Edges.Add(new CypherEdge("CONTAINS", "System", "ECommerceApp", "Module", module.Id));
        }
        return graph;
    }
}