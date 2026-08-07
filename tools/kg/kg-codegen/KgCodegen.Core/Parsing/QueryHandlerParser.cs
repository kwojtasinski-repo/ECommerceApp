using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using KgCodegen.Core.Model;

namespace KgCodegen.Core.Parsing;

public sealed class QueryHandlerParser(ModuleResolver modules)
{
    public ParserResult Parse(string infrastructureRoot, IReadOnlyList<CypherNode> queries)
    {
        var graph = Graph.Empty();
        var warnings = new List<string>();
        var resolver = new MessageNameResolver(queries, "Query", "query");
        var queryIds = queries.Select(query => query.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(infrastructureRoot, "*Handler.cs", SearchOption.AllDirectories))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetCompilationUnitRoot();
            var module = modules.Resolve(Path.GetRelativePath(infrastructureRoot, file));
            if (module is null)
            {
                continue;
            }

            foreach (var handler in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var markers = handler.BaseList?.Types
                    .Select(baseType => baseType.Type)
                    .OfType<GenericNameSyntax>()
                    .Where(type => type.Identifier.Text.Equals("IQueryHandler", StringComparison.Ordinal) &&
                                   type.TypeArgumentList.Arguments.Count == 2)
                    .ToArray() ?? [];
                if (markers.Length == 0)
                {
                    continue;
                }

                var handlerId = SyntaxNaming.FullyQualifiedName(root, handler);
                var node = new CypherNode(
                    "QueryHandler",
                    handlerId,
                    new Dictionary<string, object?>
                    {
                        ["resultType"] = markers[0].TypeArgumentList.Arguments[1].ToString()
                    });
                if (!graph.Nodes.Contains(node))
                {
                    graph.Nodes.Add(node);
                }

                AddEdgeIfMissing(graph, new CypherEdge("CONTAINS", "Module", module, "QueryHandler", handlerId));
                foreach (var marker in markers)
                {
                    var queryName = marker.TypeArgumentList.Arguments[0].ToString();
                    var resolved = resolver.Resolve(queryName, root, out var warning);
                    if (resolved is null || !queryIds.Contains(resolved))
                    {
                        warnings.Add(warning ?? $"Could not resolve handled query '{queryName}' for {handlerId} in {file}.");
                        continue;
                    }

                    AddEdgeIfMissing(graph, new CypherEdge("HANDLED_BY", "Query", resolved, "QueryHandler", handlerId));
                }
            }
        }

        return new ParserResult(graph, warnings);
    }

    private static void AddEdgeIfMissing(Graph graph, CypherEdge edge)
    {
        if (!graph.Edges.Contains(edge))
        {
            graph.Edges.Add(edge);
        }
    }
}