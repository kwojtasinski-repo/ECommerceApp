using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KgCodegen.Core.Parsing;

/// <summary>
/// The atomic roles the application actually declares, read from the `UserPermissions.Roles`
/// nested class. Nothing here is hard-coded: renaming, adding or removing a role in that class
/// changes what this catalog accepts, so a stale role name produces a warning instead of a
/// silently wrong graph.
/// </summary>
public sealed class AtomicRoleCatalog
{
    private readonly IReadOnlyDictionary<string, string> rolesByConstantName;

    private AtomicRoleCatalog(IReadOnlyDictionary<string, string> rolesByConstantName, IReadOnlyList<string> warnings)
    {
        this.rolesByConstantName = rolesByConstantName;
        Warnings = warnings;
    }

    public IReadOnlyList<string> Warnings { get; }

    public int Count => rolesByConstantName.Count;

    /// <summary>Every declared role value, in declaration order.</summary>
    public IReadOnlyList<string> Values => rolesByConstantName.Values.ToArray();

    /// <summary>`UserPermissions.Roles.Administrator` -> `"Administrator"`; null when undeclared.</summary>
    public string? ResolveConstant(string constantName) =>
        rolesByConstantName.TryGetValue(constantName, out var value) ? value : null;

    public bool IsDeclaredValue(string value) =>
        rolesByConstantName.Values.Contains(value, StringComparer.Ordinal);

    public static AtomicRoleCatalog Build(string applicationRoot)
    {
        var warnings = new List<string>();
        var roles = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(applicationRoot))
        {
            warnings.Add($"Could not read declared roles: '{applicationRoot}' does not exist.");
            return new AtomicRoleCatalog(roles, warnings);
        }

        foreach (var file in Directory.EnumerateFiles(applicationRoot, "*.cs", SearchOption.AllDirectories))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            foreach (var permissions in root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Where(declaration => declaration.Identifier.Text.Equals("UserPermissions", StringComparison.Ordinal)))
            {
                foreach (var rolesClass in permissions.Members.OfType<ClassDeclarationSyntax>()
                    .Where(declaration => declaration.Identifier.Text.Equals("Roles", StringComparison.Ordinal)))
                {
                    Collect(rolesClass, roles, warnings);
                }
            }
        }

        if (roles.Count == 0)
        {
            warnings.Add($"Could not find any 'UserPermissions.Roles' string constant under '{applicationRoot}'; no Role node can be emitted.");
        }

        return new AtomicRoleCatalog(roles, warnings);
    }

    private static void Collect(ClassDeclarationSyntax rolesClass, Dictionary<string, string> roles, List<string> warnings)
    {
        foreach (var field in rolesClass.Members.OfType<FieldDeclarationSyntax>())
        {
            if (!field.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.ConstKeyword)) ||
                field.Declaration.Type.ToString() != "string")
            {
                continue;
            }

            foreach (var variable in field.Declaration.Variables)
            {
                if (variable.Initializer?.Value is not LiteralExpressionSyntax literal ||
                    !literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    continue;
                }

                var name = variable.Identifier.Text;
                var value = literal.Token.ValueText;
                if (roles.TryGetValue(name, out var existing) && !existing.Equals(value, StringComparison.Ordinal))
                {
                    warnings.Add($"Role constant '{name}' is declared more than once with different values ('{existing}' and '{value}').");
                    continue;
                }

                roles[name] = value;
            }
        }
    }
}
