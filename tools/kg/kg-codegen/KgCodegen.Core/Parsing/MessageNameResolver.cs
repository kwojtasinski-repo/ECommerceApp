using Microsoft.CodeAnalysis.CSharp.Syntax;
using KgCodegen.Core.Model;

namespace KgCodegen.Core.Parsing;

internal sealed class MessageNameResolver
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> typesBySimpleName;
    private readonly IReadOnlySet<string> typeIds;
    private readonly string label;
    private readonly string noun;

    internal MessageNameResolver(IReadOnlyList<CypherNode> types, string label = "Message", string noun = "message")
    {
        this.label = label;
        this.noun = noun;
        typeIds = types
            .Where(type => type.Label.Equals(label, StringComparison.Ordinal))
            .Select(type => type.Id)
            .ToHashSet(StringComparer.Ordinal);
        typesBySimpleName = types
            .Where(type => type.Label.Equals(label, StringComparison.Ordinal))
            .GroupBy(type => type.Id.Split('.').Last(), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(type => type.Id).ToArray(),
                StringComparer.Ordinal);
    }

    internal string? Resolve(string simpleOrQualifiedName, CompilationUnitSyntax file, out string? warning)
    {
        warning = null;
        var aliases = file.Usings
            .Where(usingDirective => usingDirective.Alias is not null)
            .ToDictionary(
                usingDirective => usingDirective.Alias!.Name.Identifier.Text,
                usingDirective => usingDirective.Name?.ToString() ?? "",
                StringComparer.Ordinal);

        if (aliases.TryGetValue(simpleOrQualifiedName, out var aliasTarget) && typeIds.Contains(aliasTarget))
        {
            return aliasTarget;
        }

        if (simpleOrQualifiedName.Contains('.', StringComparison.Ordinal) && typeIds.Contains(simpleOrQualifiedName))
        {
            return simpleOrQualifiedName;
        }

        var candidateNamespaces = file.Usings
            .Where(usingDirective => usingDirective.Alias is null && usingDirective.Name is not null)
            .Select(usingDirective => usingDirective.Name!.ToString())
            .Concat(EnclosingNamespaceAndAncestors(file))
            .ToHashSet(StringComparer.Ordinal);
        var candidates = typesBySimpleName.TryGetValue(simpleOrQualifiedName, out var matches)
            ? matches.Where(id => candidateNamespaces.Contains(GetNamespace(id))).ToArray()
            : [];

        if (candidates.Length == 1)
        {
            return candidates[0];
        }

        if (candidates.Length == 0 && typesBySimpleName.TryGetValue(simpleOrQualifiedName, out var globalMatches))
        {
            if (globalMatches.Count == 1)
            {
                return globalMatches[0];
            }

            warning = $"Could not resolve {noun} type '{simpleOrQualifiedName}' in {file.SyntaxTree.FilePath}: multiple {noun} types match.";
            return null;
        }

        warning = $"Could not resolve {noun} type '{simpleOrQualifiedName}' in {file.SyntaxTree.FilePath}: multiple namespaces match.";
        return null;
    }

    private static IReadOnlyList<string> EnclosingNamespaceAndAncestors(CompilationUnitSyntax file)
    {
        var namespaceDeclaration = file.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        if (namespaceDeclaration is null)
        {
            return [];
        }

        var parts = namespaceDeclaration.Name.ToString().Split('.', StringSplitOptions.RemoveEmptyEntries);
        var namespaces = new List<string>();
        for (var length = parts.Length; length > 0; length--)
        {
            namespaces.Add(string.Join('.', parts.Take(length)));
        }

        return namespaces;
    }

    private static string GetNamespace(string id)
    {
        var separator = id.LastIndexOf('.');
        return separator < 0 ? "" : id[..separator];
    }
}