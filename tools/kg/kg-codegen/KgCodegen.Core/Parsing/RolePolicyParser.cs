using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using KgCodegen.Core.Model;

namespace KgCodegen.Core.Parsing;

/// <summary>
/// Emits `Role`/`Policy` nodes and `{Endpoint|Page}-[:GOVERNED_BY]->{Role|Policy}` edges from
/// `[Authorize]` attributes.
///
/// Two deliberate limits, both of which under-report rather than guess:
///
/// 1. Attribute-only. Imperative authorization — `User.IsInRole(...)` branching inside an action
///    body — is structurally invisible here. `ECommerceApp.API/Controllers/Sales/OrdersController.GetById`
///    is the concrete example: it carries only a bare class-level `[Authorize]` and does its real
///    role check in the method body, so it gets no GOVERNED_BY edge. That is an accurate reflection
///    of what a syntax-only parser can see, not a bug to paper over.
/// 2. A `Role` node exists only where some endpoint or page is actually governed by it. A role
///    declared in `UserPermissions.Roles` but never named by an `[Authorize]` attribute yields no
///    node, because an unreachable node answers none of the graph's questions. Declared roles are
///    still read from the real source (see <see cref="AtomicRoleCatalog"/>) so that a renamed or
///    added role changes resolution instead of silently producing a stale graph.
/// </summary>
public sealed class RolePolicyParser
{
    public ParserResult Parse(
        string applicationRoot,
        string apiRoot,
        string webRoot,
        IReadOnlyList<CypherNode> endpoints,
        IReadOnlyList<CypherNode> pages)
    {
        var graph = Graph.Empty();
        var warnings = new List<string>();
        var catalog = AtomicRoleCatalog.Build(applicationRoot);
        warnings.AddRange(catalog.Warnings);

        ParseProject(apiRoot, ControllerParserSupport.IsApiController, "Endpoint", endpoints, catalog, graph, warnings);
        ParseProject(webRoot, ControllerParserSupport.IsWebController, "Page", pages, catalog, graph, warnings);

        return new ParserResult(graph, warnings);
    }

    private static void ParseProject(
        string projectRoot,
        Func<ClassDeclarationSyntax, bool> isController,
        string label,
        IReadOnlyList<CypherNode> sourceNodes,
        AtomicRoleCatalog catalog,
        Graph graph,
        List<string> warnings)
    {
        var aliasIndex = RoleAliasIndex.Build(projectRoot, catalog);
        warnings.AddRange(aliasIndex.Warnings);
        var policyIndex = BuildPolicyIndex(projectRoot);
        warnings.AddRange(policyIndex.Warnings);

        var sourceIds = sourceNodes
            .Where(node => node.Label.Equals(label, StringComparison.Ordinal))
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var site in ControllerParserSupport.EnumerateActionSites(projectRoot, isController))
        {
            var classAuthorization = GetAuthorization(site.Class.AttributeLists);
            var methodAuthorization = GetAuthorization(site.Method.AttributeLists);
            var classRoles = ResolveRoles(classAuthorization.Roles, site.NodeId, aliasIndex, catalog, warnings);
            var methodRoles = ResolveRoles(methodAuthorization.Roles, site.NodeId, aliasIndex, catalog, warnings);
            var roles = CombineRoles(classRoles, methodRoles, site.NodeId, warnings);
            var policies = classAuthorization.Policies
                .Concat(methodAuthorization.Policies)
                .Select(expression => ResolvePolicy(expression, site.NodeId, policyIndex, warnings))
                .Where(value => value is not null)
                .Select(value => value!)
                .ToHashSet(StringComparer.Ordinal);

            if (roles.Values.Count == 0 && policies.Count == 0)
            {
                continue;
            }

            if (!sourceIds.Contains(site.NodeId))
            {
                warnings.Add($"GOVERNED_BY source {site.NodeId} has no matching {label} node");
                continue;
            }

            foreach (var role in roles.Values)
            {
                AddGovernance(graph, label, site.NodeId, "Role", role);
            }

            foreach (var policy in policies)
            {
                AddGovernance(graph, label, site.NodeId, "Policy", policy);
            }
        }
    }

    private static void AddGovernance(Graph graph, string sourceLabel, string sourceId, string targetLabel, string targetId)
    {
        if (!graph.Nodes.Any(node => node.Label.Equals(targetLabel, StringComparison.Ordinal) && node.Id.Equals(targetId, StringComparison.Ordinal)))
        {
            graph.Nodes.Add(new CypherNode(targetLabel, targetId, new Dictionary<string, object?>()));
        }

        var edge = new CypherEdge("GOVERNED_BY", sourceLabel, sourceId, targetLabel, targetId);
        if (!graph.Edges.Contains(edge))
        {
            graph.Edges.Add(edge);
        }
    }

    /// <summary>
    /// ASP.NET runs a class-level and a method-level `[Authorize]` as two separate filters that must
    /// both pass, and each passes if the user holds *any* of its own roles — so the set of principals
    /// that actually reach the action is the intersection, not the union.
    /// </summary>
    private static RoleConstraint CombineRoles(
        RoleConstraint classRoles,
        RoleConstraint methodRoles,
        string nodeId,
        List<string> warnings)
    {
        if (!classRoles.IsResolved || !methodRoles.IsResolved)
        {
            // The unresolved level already warned; emitting the other level's roles alone would
            // claim an access rule wider than the code's.
            return RoleConstraint.Unresolved;
        }

        if (!classRoles.IsPresent)
        {
            return methodRoles;
        }

        if (!methodRoles.IsPresent)
        {
            return classRoles;
        }

        var intersection = classRoles.Values.Intersect(methodRoles.Values, StringComparer.Ordinal).ToArray();
        if (intersection.Length == 0)
        {
            // One warning, and it says what the empty set means: no role satisfies both filters, so
            // the action is unreachable by role. Emitting no edge without saying this would look
            // identical to an action that simply carries no [Authorize(Roles = ...)].
            warnings.Add($"Authorization role intersection is empty for {nodeId}: class [{string.Join(", ", classRoles.Values)}] and method [{string.Join(", ", methodRoles.Values)}] share no role, so no principal can reach it.");
        }

        return RoleConstraint.Of(intersection);
    }

    private static RoleConstraint ResolveRoles(
        IReadOnlyList<ExpressionSyntax> expressions,
        string nodeId,
        RoleAliasIndex aliasIndex,
        AtomicRoleCatalog catalog,
        List<string> warnings)
    {
        if (expressions.Count == 0)
        {
            return RoleConstraint.Absent;
        }

        var roles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var expression in expressions)
        {
            var resolved = ResolveRoleExpression(expression, aliasIndex, catalog);
            if (resolved is null)
            {
                warnings.Add($"Could not resolve roles for {nodeId}: {expression} (no declared UserPermissions.Roles value matches).");
                return RoleConstraint.Unresolved;
            }

            foreach (var role in resolved)
            {
                roles.Add(role);
            }
        }

        return RoleConstraint.Of(roles.ToArray());
    }

    /// <summary>Null means "cannot be resolved" — never an empty list, so a caller cannot mistake
    /// an unresolved expression for "governed by nothing".</summary>
    private static IReadOnlyList<string>? ResolveRoleExpression(
        ExpressionSyntax expression,
        RoleAliasIndex aliasIndex,
        AtomicRoleCatalog catalog)
    {
        if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return SplitRoles(literal.Token.ValueText, catalog);
        }

        if (expression is IdentifierNameSyntax identifier)
        {
            var declared = catalog.ResolveConstant(identifier.Identifier.Text);
            if (declared is not null)
            {
                return [declared];
            }

            return aliasIndex.Resolve(identifier.Identifier.Text);
        }

        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            var parts = memberAccess.ToString().Split('.');
            if (parts.Length >= 3 &&
                parts[^3].Equals("UserPermissions", StringComparison.Ordinal) &&
                parts[^2].Equals("Roles", StringComparison.Ordinal))
            {
                // An atomic role reached through its declaring type — one role, never comma-split.
                var declared = catalog.ResolveConstant(parts[^1]);
                return declared is null ? null : [declared];
            }

            return aliasIndex.Resolve(memberAccess.Name.Identifier.Text);
        }

        if (expression is InterpolatedStringExpressionSyntax interpolated)
        {
            var text = new System.Text.StringBuilder();
            foreach (var content in interpolated.Contents)
            {
                if (content is InterpolatedStringTextSyntax literalText)
                {
                    text.Append(literalText.TextToken.ValueText);
                }
                else if (content is InterpolationSyntax interpolation)
                {
                    var values = ResolveRoleExpression(interpolation.Expression, aliasIndex, catalog);
                    if (values is null)
                    {
                        return null;
                    }

                    text.Append(string.Join(", ", values));
                }
            }

            return SplitRoles(text.ToString(), catalog);
        }

        return null;
    }

    /// <summary>A list containing any value that `UserPermissions.Roles` does not declare is
    /// rejected whole, rather than silently narrowed to the parts that happen to be recognised.</summary>
    private static IReadOnlyList<string>? SplitRoles(string value, AtomicRoleCatalog catalog)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(part => !catalog.IsDeclaredValue(part)))
        {
            return null;
        }

        return parts.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string? ResolvePolicy(
        ExpressionSyntax expression,
        string nodeId,
        PolicyIndex policyIndex,
        List<string> warnings)
    {
        string? value = expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
            IdentifierNameSyntax identifier => policyIndex.Resolve(identifier.Identifier.Text),
            MemberAccessExpressionSyntax memberAccess => policyIndex.Resolve(memberAccess.Name.Identifier.Text),
            _ => null
        };

        if (value is null)
        {
            warnings.Add($"Could not resolve policy for {nodeId}: {expression}");
        }

        return value;
    }

    private static Authorization GetAuthorization(SyntaxList<AttributeListSyntax> attributes)
    {
        var roles = new List<ExpressionSyntax>();
        var policies = new List<ExpressionSyntax>();
        foreach (var attribute in attributes.SelectMany(list => list.Attributes))
        {
            // Exact name match: `[OutputCache(PolicyName = ...)]` is output caching, not
            // authorization, and must never reach the graph.
            if (!AttributeName(attribute.Name).Equals("Authorize", StringComparison.Ordinal) || attribute.ArgumentList is null)
            {
                continue;
            }

            foreach (var argument in attribute.ArgumentList.Arguments)
            {
                var name = argument.NameEquals?.Name.Identifier.Text;
                if (name is not null && name.Equals("Roles", StringComparison.Ordinal))
                {
                    roles.Add(argument.Expression);
                }
                else if (name is not null && name.Equals("Policy", StringComparison.Ordinal))
                {
                    policies.Add(argument.Expression);
                }
            }
        }

        return new Authorization(roles, policies);
    }

    private static string AttributeName(NameSyntax name)
    {
        var value = name.ToString();
        return value.EndsWith("Attribute", StringComparison.Ordinal) ? value[..^9] : value;
    }

    private static PolicyIndex BuildPolicyIndex(string projectRoot)
    {
        var values = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                if (!field.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword)) ||
                    !field.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.ConstKeyword)) ||
                    !field.Declaration.Type.ToString().Equals("string", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var variable in field.Declaration.Variables)
                {
                    if (variable.Initializer?.Value is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        if (!values.TryGetValue(variable.Identifier.Text, out var candidates))
                        {
                            candidates = new HashSet<string>(StringComparer.Ordinal);
                            values[variable.Identifier.Text] = candidates;
                        }

                        candidates.Add(literal.Token.ValueText);
                    }
                }
            }
        }

        var warnings = values.Where(pair => pair.Value.Count > 1)
            .Select(pair => $"Policy constant '{pair.Key}' has conflicting declarations and cannot be resolved.")
            .ToArray();
        var resolved = values
            .Where(pair => pair.Value.Count == 1)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Single(), StringComparer.Ordinal);
        return new PolicyIndex(resolved, warnings);
    }

    private sealed record Authorization(IReadOnlyList<ExpressionSyntax> Roles, IReadOnlyList<ExpressionSyntax> Policies);

    private sealed record RoleConstraint(bool IsPresent, bool IsResolved, IReadOnlyList<string> Values)
    {
        public static readonly RoleConstraint Absent = new(false, true, []);
        public static readonly RoleConstraint Unresolved = new(true, false, []);

        public static RoleConstraint Of(IReadOnlyList<string> values) => new(true, true, values);
    }

    private sealed record PolicyIndex(IReadOnlyDictionary<string, string> Values, IReadOnlyList<string> Warnings)
    {
        public string? Resolve(string name) => Values.TryGetValue(name, out var value) ? value : null;
    }
}
