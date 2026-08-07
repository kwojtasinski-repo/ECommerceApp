using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using KgCodegen.Core.Model;

namespace KgCodegen.Core.Parsing;

public sealed class ActionParser(ModuleResolver modules)
{
    public ParserResult Parse(string applicationRoot)
    {
        var graph = Graph.Empty();
        foreach (var file in Directory.EnumerateFiles(applicationRoot, ServiceActionSites.ServiceFilePattern, SearchOption.AllDirectories))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            var module = modules.Resolve(Path.GetRelativePath(applicationRoot, file));
            if (module is null)
            {
                continue;
            }

            foreach (var site in ServiceActionSites.Enumerate(root))
            {
                graph.Nodes.Add(new CypherNode("Action", site.ActionId, new Dictionary<string, object?>()));
                graph.Edges.Add(new CypherEdge("CONTAINS", "Module", module, "Action", site.ActionId));
            }
        }
        return new ParserResult(graph, []);
    }
}