using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KgCodegen.Core.Parsing;

/// <summary>
/// The one place a syntax node is turned into the `{namespace}.{TypeName}` id the graph uses.
/// Every parser that names a type reads through here, so two parsers cannot drift into disagreeing
/// about what a given declaration is called — which would silently split one node into two.
/// </summary>
internal static class SyntaxNaming
{
    /// <summary>
    /// `{namespace}.{TypeName}`, or the bare type name when the declaration sits outside any
    /// namespace. The enclosing namespace is the one whose span actually contains the declaration,
    /// so a file with several namespace blocks names each type correctly.
    /// </summary>
    internal static string FullyQualifiedName(CompilationUnitSyntax root, TypeDeclarationSyntax type)
    {
        var namespaceName = root.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault(namespaceDeclaration => namespaceDeclaration.Span.Contains(type.SpanStart))
            ?.Name.ToString();
        return string.IsNullOrEmpty(namespaceName) ? type.Identifier.Text : $"{namespaceName}.{type.Identifier.Text}";
    }
}
