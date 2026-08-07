using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KgCodegen.Core.Parsing;

public sealed class DomainSymbolIndex
{
    private readonly Dictionary<string, string> bySimpleName = new(StringComparer.Ordinal);
    public List<string> Warnings { get; } = [];

    public static DomainSymbolIndex Build(string domainRoot)
    {
        var index = new DomainSymbolIndex();
        foreach (var file in Directory.EnumerateFiles(domainRoot, "*.cs", SearchOption.AllDirectories))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            var ns = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? "";
            foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var fqcn = string.IsNullOrEmpty(ns) ? type.Identifier.Text : ns + "." + type.Identifier.Text;
                if (!index.bySimpleName.TryAdd(type.Identifier.Text, fqcn))
                    index.Warnings.Add($"Duplicate type name '{type.Identifier.Text}', keeping '{index.bySimpleName[type.Identifier.Text]}'.");
            }
        }
        return index;
    }

    public string? Resolve(string simpleName) => bySimpleName.GetValueOrDefault(simpleName.Trim().Split('.').Last().TrimEnd('?'));
}