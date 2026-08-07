using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using KgCodegen.Core.Model;

namespace KgCodegen.Core.Parsing;

public sealed class ActionParser(ModuleResolver modules)
{
    public ParserResult Parse(string applicationRoot)
    {
        var graph = Graph.Empty();
        foreach (var file in Directory.EnumerateFiles(applicationRoot, "*Service.cs", SearchOption.AllDirectories))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            var ns = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? "";
            var module = modules.Resolve(Path.GetRelativePath(applicationRoot, file));
            if (module is null) continue;
            foreach (var service in root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(x => x.Identifier.Text.EndsWith("Service", StringComparison.Ordinal)))
            {
                var classId = string.IsNullOrEmpty(ns) ? service.Identifier.Text : ns + "." + service.Identifier.Text;
                foreach (var method in service.Members.OfType<MethodDeclarationSyntax>().Where(x => x.Modifiers.Any(modifier => modifier.RawKind == (int)SyntaxKind.PublicKeyword)))
                {
                    var id = classId + "." + method.Identifier.Text;
                    graph.Nodes.Add(new CypherNode("Action", id, new Dictionary<string, object?>()));
                    graph.Edges.Add(new CypherEdge("CONTAINS", "Module", module, "Action", id));
                }
            }
        }
        return new ParserResult(graph, []);
    }
}