using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using KgCodegen.Core.Model;

namespace KgCodegen.Core.Parsing;

public sealed class RepositoryParser(ModuleResolver modules)
{
    public ParserResult Parse(string domainRoot, IReadOnlyList<CypherNode> entities)
    {
        var graph = Graph.Empty();
        var warnings = new List<string>();
        var entityByName = entities.ToDictionary(x => x.Id.Split('.').Last(), StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(domainRoot, "I*Repository.cs", SearchOption.AllDirectories))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
            var ns = root.GetRoot().DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? "";
            foreach (var declaration in root.GetRoot().DescendantNodes().OfType<InterfaceDeclarationSyntax>())
            {
                var id = string.IsNullOrEmpty(ns) ? declaration.Identifier.Text : ns + "." + declaration.Identifier.Text;
                graph.Nodes.Add(new CypherNode("Repository", id, new Dictionary<string, object?>()));
                var module = modules.Resolve(Path.GetRelativePath(domainRoot, file));
                if (module is not null) graph.Edges.Add(new CypherEdge("CONTAINS", "Module", module, "Repository", id));
                var referencedTypeNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>())
                {
                    CollectLeafTypeNames(method.ReturnType, referencedTypeNames);
                    foreach (var parameter in method.ParameterList.Parameters)
                        if (parameter.Type is not null) CollectLeafTypeNames(parameter.Type, referencedTypeNames);
                }
                foreach (var entity in entityByName.Where(x => referencedTypeNames.Contains(x.Key)))
                    graph.Edges.Add(new CypherEdge("PERSISTED_BY", "Entity", entity.Value.Id, "Repository", id));
            }
        }
        return new ParserResult(graph, warnings);
    }

    // Unwraps Task<T>, IReadOnlyList<T>, T?, T[], and Namespace.T down to the leaf simple
    // type name(s) actually referenced, so matching against entity names is exact-equality,
    // never substring — "CouponUsed" must never match entity "Coupon" just because it starts
    // with the same letters.
    private static void CollectLeafTypeNames(TypeSyntax? type, HashSet<string> into)
    {
        switch (type)
        {
            case null:
                return;
            case NullableTypeSyntax nullable:
                CollectLeafTypeNames(nullable.ElementType, into);
                return;
            case ArrayTypeSyntax array:
                CollectLeafTypeNames(array.ElementType, into);
                return;
            case GenericNameSyntax generic:
                foreach (var arg in generic.TypeArgumentList.Arguments) CollectLeafTypeNames(arg, into);
                return;
            case QualifiedNameSyntax qualified:
                into.Add(qualified.Right.Identifier.Text);
                return;
            case IdentifierNameSyntax identifier:
                into.Add(identifier.Identifier.Text);
                return;
        }
    }
}