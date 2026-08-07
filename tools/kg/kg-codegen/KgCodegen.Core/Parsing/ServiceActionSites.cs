using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KgCodegen.Core.Parsing;

/// <summary>
/// A public method of a `*Service` class — the shape an `Action` node is made from.
/// <paramref name="ActionId"/> is the id that method's `Action` node carries.
/// </summary>
internal sealed record ServiceActionSite(string ClassId, string ActionId, MethodDeclarationSyntax Method);

/// <summary>
/// The single definition of "which method becomes an `Action`, and what is its id".
///
/// `ActionParser` creates the `Action` nodes; `MessageParser` emits `Action-[:PUBLISHES]->Message`
/// edges that must point at exactly those ids. Before this helper existed both derived the id
/// themselves, so any change to the id shape had to be made twice or the `PUBLISHES` edges would
/// quietly stop matching. Phases 4b/4c add more `Action`-sourced edges and read through here too.
/// </summary>
internal static class ServiceActionSites
{
    /// <summary>The file pattern whose classes can hold actions. Callers enumerate with it so the
    /// scan root and this helper cannot disagree about which files are service files.</summary>
    internal const string ServiceFilePattern = "*Service.cs";

    internal static IEnumerable<ServiceActionSite> Enumerate(SyntaxNode root)
    {
        var namespaceName = root.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault()?.Name.ToString() ?? "";
        foreach (var service in root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Where(type => type.Identifier.Text.EndsWith("Service", StringComparison.Ordinal)))
        {
            var classId = string.IsNullOrEmpty(namespaceName)
                ? service.Identifier.Text
                : namespaceName + "." + service.Identifier.Text;
            foreach (var method in service.Members.OfType<MethodDeclarationSyntax>()
                .Where(method => method.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword))))
            {
                yield return new ServiceActionSite(classId, classId + "." + method.Identifier.Text, method);
            }
        }
    }
}
