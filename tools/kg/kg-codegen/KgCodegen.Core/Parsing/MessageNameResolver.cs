using Microsoft.CodeAnalysis.CSharp.Syntax;
using KgCodegen.Core.Model;

namespace KgCodegen.Core.Parsing;

internal sealed class MessageNameResolver
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> messagesBySimpleName;
    private readonly IReadOnlySet<string> messageIds;

    internal MessageNameResolver(IReadOnlyList<CypherNode> messages)
    {
        messageIds = messages
            .Where(message => message.Label.Equals("Message", StringComparison.Ordinal))
            .Select(message => message.Id)
            .ToHashSet(StringComparer.Ordinal);
        messagesBySimpleName = messages
            .Where(message => message.Label.Equals("Message", StringComparison.Ordinal))
            .GroupBy(message => message.Id.Split('.').Last(), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(message => message.Id).ToArray(),
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

        if (aliases.TryGetValue(simpleOrQualifiedName, out var aliasTarget) && messageIds.Contains(aliasTarget))
        {
            return aliasTarget;
        }

        if (simpleOrQualifiedName.Contains('.', StringComparison.Ordinal) && messageIds.Contains(simpleOrQualifiedName))
        {
            return simpleOrQualifiedName;
        }

        var candidateNamespaces = file.Usings
            .Where(usingDirective => usingDirective.Alias is null && usingDirective.Name is not null)
            .Select(usingDirective => usingDirective.Name!.ToString())
            .Concat(EnclosingNamespaceAndAncestors(file))
            .ToHashSet(StringComparer.Ordinal);
        var candidates = messagesBySimpleName.TryGetValue(simpleOrQualifiedName, out var matches)
            ? matches.Where(id => candidateNamespaces.Contains(GetNamespace(id))).ToArray()
            : [];

        if (candidates.Length == 1)
        {
            return candidates[0];
        }

        if (candidates.Length == 0 && messagesBySimpleName.TryGetValue(simpleOrQualifiedName, out var globalMatches))
        {
            if (globalMatches.Count == 1)
            {
                return globalMatches[0];
            }

            warning = $"Could not resolve message type '{simpleOrQualifiedName}' in {file.SyntaxTree.FilePath}: multiple message types match.";
            return null;
        }

        warning = $"Could not resolve message type '{simpleOrQualifiedName}' in {file.SyntaxTree.FilePath}: multiple namespaces match.";
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