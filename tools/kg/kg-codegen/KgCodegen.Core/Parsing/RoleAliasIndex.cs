using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KgCodegen.Core.Parsing;

/// <summary>
/// Resolves a project's role alias constants (`MaintenanceRole`, `ManagingRole`, ...) down to the
/// atomic roles they are composed of. Built once per project root, never shared: the API and the
/// Web project declare the same alias names independently, so a single global lookup would report
/// a wrong graph the moment one of them is edited and the other is not.
/// </summary>
public sealed class RoleAliasIndex
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> aliases;

    private RoleAliasIndex(
        IReadOnlyDictionary<string, IReadOnlyList<string>> aliases,
        IReadOnlyList<string> warnings)
    {
        this.aliases = aliases;
        Warnings = warnings;
    }

    public IReadOnlyList<string> Warnings { get; }

    public int Count => aliases.Count;

    public IReadOnlyList<string>? Resolve(string aliasName) =>
        aliases.TryGetValue(aliasName, out var values) ? values : null;

    public static RoleAliasIndex Build(string projectRoot, AtomicRoleCatalog catalog)
    {
        var declarations = new Dictionary<string, List<VariableDeclaratorSyntax>>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                if (!field.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword)) ||
                    !field.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.ConstKeyword)) ||
                    field.Declaration.Type.ToString() != "string")
                {
                    continue;
                }

                foreach (var variable in field.Declaration.Variables)
                {
                    if (variable.Initializer is not null)
                    {
                        if (!declarations.TryGetValue(variable.Identifier.Text, out var values))
                        {
                            values = [];
                            declarations[variable.Identifier.Text] = values;
                        }

                        values.Add(variable);
                    }
                }
            }
        }

        var warnings = new List<string>();
        var resolved = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var ambiguous = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, variables) in declarations)
        {
            var candidates = variables
                .Select(variable => Evaluate(variable.Initializer!.Value, declarations, catalog, new HashSet<string>(StringComparer.Ordinal)))
                .Where(values => values is not null)
                .Select(values => values!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (candidates.Length == 0)
            {
                continue;
            }

            if (candidates.Length > 1)
            {
                warnings.Add($"Role alias '{name}' has conflicting declarations and cannot be resolved.");
                ambiguous.Add(name);
                continue;
            }

            resolved[name] = candidates[0].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }

        foreach (var name in ambiguous)
        {
            resolved.Remove(name);
        }

        return new RoleAliasIndex(resolved, warnings);
    }

    private static string? Evaluate(
        ExpressionSyntax expression,
        IReadOnlyDictionary<string, List<VariableDeclaratorSyntax>> declarations,
        AtomicRoleCatalog catalog,
        HashSet<string> resolving)
    {
        if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return IsDeclaredRoleList(literal.Token.ValueText, catalog) ? literal.Token.ValueText : null;
        }

        if (expression is IdentifierNameSyntax identifier &&
            declarations.TryGetValue(identifier.Identifier.Text, out var variables) &&
            variables.Count == 1 && resolving.Add(identifier.Identifier.Text))
        {
            var result = Evaluate(variables[0].Initializer!.Value, declarations, catalog, resolving);
            resolving.Remove(identifier.Identifier.Text);
            return result;
        }

        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            var parts = memberAccess.ToString().Split('.');
            if (parts.Length >= 3 &&
                parts[^3].Equals("UserPermissions", StringComparison.Ordinal) &&
                parts[^2].Equals("Roles", StringComparison.Ordinal))
            {
                return catalog.ResolveConstant(parts[^1]);
            }

            return Evaluate(memberAccess.Name, declarations, catalog, resolving);
        }

        if (expression is InterpolatedStringExpressionSyntax interpolated)
        {
            var text = new System.Text.StringBuilder();
            var hasRoleHole = false;
            foreach (var content in interpolated.Contents)
            {
                if (content is InterpolatedStringTextSyntax literalText)
                {
                    text.Append(literalText.TextToken.ValueText);
                    continue;
                }

                if (content is InterpolationSyntax interpolation)
                {
                    var value = Evaluate(interpolation.Expression, declarations, catalog, resolving);
                    if (value is null)
                    {
                        return null;
                    }

                    hasRoleHole = true;
                    text.Append(value);
                }
            }

            return hasRoleHole && IsDeclaredRoleList(text.ToString(), catalog) ? text.ToString() : null;
        }

        return null;
    }

    private static bool IsDeclaredRoleList(string value, AtomicRoleCatalog catalog)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && parts.All(catalog.IsDeclaredValue);
    }
}
