using KgCodegen.Core.Model;
using KgCodegen.Core.Overrides;

namespace KgCodegen.Core.Spine;

public static class SpineCatalog
{
    public static Graph Create(IReadOnlyList<ModuleOverride> modules)
    {
        var graph = Graph.Empty();
        graph.Nodes.Add(new CypherNode("System", "ECommerceApp", new Dictionary<string, object?>()));
        graph.Nodes.Add(new CypherNode("Host", "ApiHost", new Dictionary<string, object?> { ["path"] = "ECommerceApp.API" }));
        graph.Nodes.Add(new CypherNode("Host", "WebHost", new Dictionary<string, object?> { ["path"] = "ECommerceApp.Web" }));
        graph.Edges.Add(new CypherEdge("CONTAINS", "System", "ECommerceApp", "Host", "ApiHost"));
        graph.Edges.Add(new CypherEdge("CONTAINS", "System", "ECommerceApp", "Host", "WebHost"));
        // Hosts and the system are stable spine facts; only modules need generation-time overrides.
        foreach (var module in modules)
        {
            graph.Nodes.Add(new CypherNode("Module", module.Id, new Dictionary<string, object?> { ["path"] = module.Path }));
            graph.Edges.Add(new CypherEdge("CONTAINS", "System", "ECommerceApp", "Module", module.Id));
        }
        return graph;
    }
}