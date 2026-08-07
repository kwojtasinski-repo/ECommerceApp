using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using KgCodegen.Core.Model;

namespace KgCodegen.Core.Parsing;

internal readonly record struct ControllerActionSite(
    string ClassId,
    ClassDeclarationSyntax Class,
    MethodDeclarationSyntax Method,
    string NodeId);

internal static class ControllerParserSupport
{
    public static ParserResult Parse(
        string rootPath,
        string hostId,
        string label,
        Func<ClassDeclarationSyntax, bool> isController,
        DomainSymbolIndex applicationSymbols,
        IReadOnlyList<CypherNode> actions)
    {
        var graph = Graph.Empty();
        var warnings = new List<string>();
        var actionIds = actions.Where(node => node.Label == "Action")
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);

        // Sites arrive grouped by controller, so the two pieces of per-class work — the injected
        // service fields and the class-level [Route] — are computed once per class rather than
        // once per action.
        ClassDeclarationSyntax? currentClass = null;
        var serviceFields = new Dictionary<string, string>(StringComparer.Ordinal);
        string? classRoute = null;

        foreach (var site in EnumerateActionSites(rootPath, isController))
        {
            var classId = site.ClassId;
            var controller = site.Class;
            var method = site.Method;
            if (!ReferenceEquals(currentClass, controller))
            {
                currentClass = controller;
                serviceFields = controller.Members.OfType<FieldDeclarationSyntax>()
                    .SelectMany(field => field.Declaration.Variables.Select(variable =>
                        (Field: variable.Identifier.Text, Interface: GetSimpleTypeName(field.Declaration.Type))))
                    .Where(pair => pair.Interface is not null && pair.Interface.StartsWith("I", StringComparison.Ordinal) && pair.Interface.EndsWith("Service", StringComparison.Ordinal))
                    .ToDictionary(pair => pair.Field, pair => pair.Interface!, StringComparer.Ordinal);
                classRoute = GetStringAttributeValue(controller.AttributeLists, "Route");
            }

            var httpMethod = GetHttpMethod(method.AttributeLists);
            var route = CombineRoute(classRoute, GetMethodRoute(method.AttributeLists));
            if (route is null)
            {
                warnings.Add($"Could not confidently extract route for {classId}.{method.Identifier.Text}.");
            }

            graph.Nodes.Add(new CypherNode(label, site.NodeId, new Dictionary<string, object?>
            {
                ["httpMethod"] = httpMethod,
                ["route"] = route
            }));
            graph.Edges.Add(new CypherEdge("CONTAINS", "Host", hostId, label, site.NodeId));

            foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
                    memberAccess.Expression is not IdentifierNameSyntax receiver ||
                    !serviceFields.TryGetValue(receiver.Identifier.Text, out var interfaceName))
                {
                    continue;
                }

                // `IOrderService` -> `OrderService` is the convention; the interface lookup is the
                // fallback for decorators (`CachedCatalogNavigationService : ICatalogNavigationService`).
                // Both refuse to answer when the name is declared more than once, so an ambiguous
                // service produces a warning rather than an edge to an arbitrarily picked class.
                var concreteName = interfaceName[1..];
                var ambiguous = applicationSymbols.IsAmbiguous(concreteName) || applicationSymbols.IsAmbiguous(interfaceName);
                var concreteType = ambiguous
                    ? null
                    : applicationSymbols.Resolve(concreteName) ?? applicationSymbols.ResolveImplementation(interfaceName);
                var actionId = concreteType is null ? null : concreteType + "." + memberAccess.Name.Identifier.Text;
                if (actionId is null || !actionIds.Contains(actionId))
                {
                    var reason = ambiguous ? " (more than one type declares that name)" : "";
                    warnings.Add($"Could not resolve action for {classId}.{method.Identifier.Text}: {interfaceName}.{memberAccess.Name.Identifier.Text}{reason}.");
                    continue;
                }

                graph.Edges.Add(new CypherEdge("EXPOSED_BY", "Action", actionId, label, site.NodeId));
            }
        }

        return new ParserResult(graph, warnings);
    }

    internal static IEnumerable<ControllerActionSite> EnumerateActionSites(
        string rootPath,
        Func<ClassDeclarationSyntax, bool> isController)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(rootPath, "*Controller.cs", SearchOption.AllDirectories))
        {
            var syntaxRoot = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            var ns = syntaxRoot.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? "";
            foreach (var controller in syntaxRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(isController))
            {
                var classId = string.IsNullOrEmpty(ns) ? controller.Identifier.Text : ns + "." + controller.Identifier.Text;
                foreach (var method in controller.Members.OfType<MethodDeclarationSyntax>()
                    .Where(method => method.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword))))
                {
                    var nodeId = UniqueId(ids, classId + "." + method.Identifier.Text);
                    yield return new ControllerActionSite(classId, controller, method, nodeId);
                }
            }
        }
    }

    private static bool HasAttribute(SyntaxList<AttributeListSyntax> attributes, string name) =>
        attributes.SelectMany(list => list.Attributes)
            .Any(attribute => AttributeName(attribute.Name).Equals(name, StringComparison.Ordinal));

    public static bool IsApiController(ClassDeclarationSyntax controller) =>
        controller.Identifier.Text.EndsWith("Controller", StringComparison.Ordinal) &&
        (HasAttribute(controller.AttributeLists, "ApiController") ||
         controller.BaseList?.Types.Any(type => type.Type.ToString() is "BaseController" or "ControllerBase") == true);

    public static bool IsWebController(ClassDeclarationSyntax controller) =>
        controller.Identifier.Text.EndsWith("Controller", StringComparison.Ordinal) &&
        controller.BaseList?.Types.Any(type => type.Type.ToString() is "BaseController" or "Controller") == true;

    private static string? GetHttpMethod(SyntaxList<AttributeListSyntax> attributes)
    {
        var attribute = attributes.SelectMany(list => list.Attributes)
            .Select(attribute => AttributeName(attribute.Name))
            .FirstOrDefault(name => name.StartsWith("Http", StringComparison.Ordinal) && name.Length > 4);
        return attribute?[4..].ToUpperInvariant();
    }

    private static string? GetStringAttributeValue(SyntaxList<AttributeListSyntax> attributes, string name)
    {
        var attribute = attributes.SelectMany(list => list.Attributes)
            .FirstOrDefault(attribute => AttributeName(attribute.Name).Equals(name, StringComparison.Ordinal));
        return attribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal.Token.ValueText
            : null;
    }

    private static string? GetMethodRoute(SyntaxList<AttributeListSyntax> attributes)
    {
        var explicitRoute = GetStringAttributeValue(attributes, "Route");
        if (explicitRoute is not null)
        {
            return explicitRoute;
        }

        var httpAttribute = attributes.SelectMany(list => list.Attributes)
            .FirstOrDefault(attribute => AttributeName(attribute.Name).StartsWith("Http", StringComparison.Ordinal));
        return httpAttribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal.Token.ValueText
            : null;
    }

    private static string? CombineRoute(string? classRoute, string? methodRoute)
    {
        if (classRoute is null)
        {
            return methodRoute;
        }

        if (methodRoute is null)
        {
            return classRoute;
        }

        var tail = methodRoute.Trim('/');
        return tail.Length == 0 ? classRoute.TrimEnd('/') : classRoute.TrimEnd('/') + "/" + tail;
    }

    private static string AttributeName(NameSyntax name)
    {
        var value = name.ToString();
        return value.EndsWith("Attribute", StringComparison.Ordinal) ? value[..^9] : value;
    }

    private static string? GetSimpleTypeName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
        GenericNameSyntax generic => generic.Identifier.Text,
        _ => null
    };

    private static string UniqueId(HashSet<string> ids, string baseId)
    {
        if (ids.Add(baseId))
        {
            return baseId;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = baseId + "#" + suffix;
            if (ids.Add(candidate))
            {
                return candidate;
            }
        }
    }
}