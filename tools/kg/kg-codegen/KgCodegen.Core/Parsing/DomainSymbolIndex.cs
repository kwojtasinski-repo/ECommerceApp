using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KgCodegen.Core.Parsing;

public sealed class DomainSymbolIndex
{
    private readonly Dictionary<string, string> bySimpleName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> byImplementedInterface = new(StringComparer.Ordinal);
    private readonly HashSet<string> ambiguousSimpleNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> ambiguousInterfaces = new(StringComparer.Ordinal);
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
                {
                    index.ambiguousSimpleNames.Add(type.Identifier.Text);
                    index.Warnings.Add($"Duplicate type name '{type.Identifier.Text}', keeping '{index.bySimpleName[type.Identifier.Text]}'.");
                }

                if (type is not ClassDeclarationSyntax classDeclaration || classDeclaration.BaseList is null)
                {
                    continue;
                }

                foreach (var implemented in classDeclaration.BaseList.Types
                    .Select(baseType => Simplify(baseType.Type.ToString()))
                    .Where(IsInterfaceName))
                {
                    if (!index.byImplementedInterface.TryAdd(implemented, fqcn))
                    {
                        index.ambiguousInterfaces.Add(implemented);
                    }
                }
            }
        }
        return index;
    }

    public string? Resolve(string simpleName) => bySimpleName.GetValueOrDefault(Simplify(simpleName));

    /// <summary>
    /// Resolves the single class implementing <paramref name="interfaceName"/>. Returns null when
    /// no class implements it, and also when more than one does — an ambiguous interface is never
    /// guessed, so callers warn instead of fabricating an edge to an arbitrary implementation.
    /// </summary>
    public string? ResolveImplementation(string interfaceName)
    {
        var key = Simplify(interfaceName);
        return ambiguousInterfaces.Contains(key) ? null : byImplementedInterface.GetValueOrDefault(key);
    }

    /// <summary>
    /// True when the name is declared by more than one type, so any <see cref="Resolve"/> or
    /// <see cref="ResolveImplementation"/> hit for it would be an arbitrary pick.
    /// </summary>
    public bool IsAmbiguous(string name)
    {
        var key = Simplify(name);
        return ambiguousSimpleNames.Contains(key) || ambiguousInterfaces.Contains(key);
    }

    private static string Simplify(string name) => name.Trim().Split('.').Last().TrimEnd('?');

    private static bool IsInterfaceName(string name) => name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]);
}
