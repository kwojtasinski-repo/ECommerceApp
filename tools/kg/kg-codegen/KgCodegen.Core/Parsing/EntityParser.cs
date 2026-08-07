using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using KgCodegen.Core.Model;

namespace KgCodegen.Core.Parsing;

public sealed class EntityParser(ModuleResolver modules)
{
    public ParserResult Parse(string infrastructureRoot, DomainSymbolIndex symbols)
    {
        var graph = Graph.Empty();
        var warnings = new List<string>();
        foreach (var file in Directory.EnumerateFiles(infrastructureRoot, "*Configuration.cs", SearchOption.AllDirectories))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            var module = modules.Resolve(Path.GetRelativePath(infrastructureRoot, file));
            if (module is null) continue;
            foreach (var type in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var marker = type.BaseList?.Types.FirstOrDefault(x => x.Type.ToString().StartsWith("IEntityTypeConfiguration<", StringComparison.Ordinal));
                if (marker is null) continue;
                var simple = marker.Type.ToString()["IEntityTypeConfiguration<".Length..].TrimEnd('>');
                var id = symbols.Resolve(simple) ?? simple;
                var table = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .FirstOrDefault(x => x.Expression.ToString().EndsWith("ToTable", StringComparison.Ordinal))?.ArgumentList.Arguments.FirstOrDefault()?.Expression as LiteralExpressionSyntax;
                if (table is null) warnings.Add($"No ToTable literal found in {file} for {simple}.");
                graph.Nodes.Add(new CypherNode("Entity", id, new Dictionary<string, object?> { ["table"] = table?.Token.ValueText }));
                graph.Edges.Add(new CypherEdge("CONTAINS", "Module", module, "Entity", id));
            }
        }
        return new ParserResult(graph, warnings);
    }
}