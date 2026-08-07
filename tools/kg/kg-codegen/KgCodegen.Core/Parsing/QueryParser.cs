using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using KgCodegen.Core.Model;

namespace KgCodegen.Core.Parsing;

public sealed class QueryParser
{
    /// <summary>
    /// Derived from <see cref="ServiceActionSites.ServiceFilePattern"/> rather than written out, so
    /// the set of files this parser treats as service files cannot drift from the set
    /// <see cref="ActionParser"/> builds `Action` nodes from. A literal here would silently start
    /// disagreeing the moment that pattern changes. It is a suffix test rather than a second
    /// <see cref="Directory.EnumerateFiles(string, string, SearchOption)"/> pass because query
    /// discovery already needs every `*.cs` file, and the tree is walked once.
    /// </summary>
    private static readonly string ServiceFileSuffix = ServiceActionSites.ServiceFilePattern.TrimStart('*');

    public ParserResult Parse(string applicationRoot, IReadOnlyList<CypherNode> actions)
    {
        var graph = Graph.Empty();
        var warnings = new List<string>();
        var querySyntax = new List<(CompilationUnitSyntax Root, TypeDeclarationSyntax Type)>();
        var serviceRoots = new List<CompilationUnitSyntax>();

        foreach (var file in Directory.EnumerateFiles(applicationRoot, "*.cs", SearchOption.AllDirectories))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetCompilationUnitRoot();
            if (file.EndsWith(ServiceFileSuffix, StringComparison.Ordinal))
            {
                serviceRoots.Add(root);
            }

            foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (type.BaseList?.Types.Any(baseType => baseType.Type is GenericNameSyntax generic &&
                    generic.Identifier.Text.Equals("IQuery", StringComparison.Ordinal) &&
                    generic.TypeArgumentList.Arguments.Count == 1) == true)
                {
                    querySyntax.Add((root, type));
                }
            }
        }

        var queries = querySyntax
            .Select(item =>
            {
                var marker = item.Type.BaseList!.Types
                    .Select(baseType => baseType.Type)
                    .OfType<GenericNameSyntax>()
                    .First(type => type.Identifier.Text.Equals("IQuery", StringComparison.Ordinal) &&
                                   type.TypeArgumentList.Arguments.Count == 1);
                return new CypherNode(
                    "Query",
                    SyntaxNaming.FullyQualifiedName(item.Root, item.Type),
                    new Dictionary<string, object?>
                    {
                        ["resultType"] = marker.TypeArgumentList.Arguments[0].ToString()
                    });
            })
            .ToArray();

        foreach (var query in queries)
        {
            if (!graph.Nodes.Contains(query))
            {
                graph.Nodes.Add(query);
            }
        }

        // USES covers **3 of the 5** real query-send sites (60 %). **1 of 3** `Query` nodes
        // (`OrderExistsQuery`) carries any `USES` in-edge. `StockAvailableQuery` and
        // `CompletedOrderCountQuery` are sent only from `ECommerceApp.Infrastructure/Sales/Coupons/Adapters/`,
        // which produces no `Action` node. Closing the gap requires `Action` to cover Infrastructure
        // Adapter classes — a deliberate scope decision, deferred.
        var actionIds = actions
            .Where(action => action.Label.Equals("Action", StringComparison.Ordinal))
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        var resolver = new MessageNameResolver(queries, "Query", "query");
        var queryIds = queries.Select(query => query.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var root in serviceRoots)
        {
            foreach (var site in ServiceActionSites.Enumerate(root))
            {
                if (!actionIds.Contains(site.ActionId))
                {
                    continue;
                }

                var moduleClientFields = FindModuleClientFields(site.Method);
                foreach (var invocation in site.Method.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
                        !memberAccess.Name.Identifier.Text.Equals("SendAsync", StringComparison.Ordinal) ||
                        memberAccess.Expression is not IdentifierNameSyntax receiver ||
                        !moduleClientFields.Contains(receiver.Identifier.Text))
                    {
                        continue;
                    }

                    var argument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
                    var queryName = argument switch
                    {
                        ObjectCreationExpressionSyntax creation => creation.Type.ToString(),
                        IdentifierNameSyntax identifier => FindLocalQueryType(site.Method, identifier.Identifier.Text),
                        _ => null
                    };
                    if (queryName is null)
                    {
                        warnings.Add($"Could not extract query in {site.ActionId}: {argument ?? invocation}.");
                        continue;
                    }

                    var resolved = resolver.Resolve(queryName, root, out var warning);
                    if (resolved is null || !queryIds.Contains(resolved))
                    {
                        warnings.Add(warning ?? $"Could not resolve query type '{queryName}' in {root.SyntaxTree.FilePath}.");
                        continue;
                    }

                    var edge = new CypherEdge("USES", "Action", site.ActionId, "Query", resolved);
                    if (!graph.Edges.Contains(edge))
                    {
                        graph.Edges.Add(edge);
                    }
                }
            }
        }

        return new ParserResult(graph, warnings);
    }

    private static IReadOnlySet<string> FindModuleClientFields(MethodDeclarationSyntax method)
    {
        var containingClass = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var fields = containingClass.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(field => GetSimpleTypeName(field.Declaration.Type).Equals("IModuleClient", StringComparison.Ordinal))
            .SelectMany(field => field.Declaration.Variables.Select(variable => variable.Identifier.Text));
        var constructorParameters = containingClass.Members
            .OfType<ConstructorDeclarationSyntax>()
            .SelectMany(constructor => constructor.ParameterList.Parameters)
            .Where(parameter => parameter.Type is not null &&
                                GetSimpleTypeName(parameter.Type).Equals("IModuleClient", StringComparison.Ordinal))
            .Select(parameter => parameter.Identifier.Text);
        return fields.Concat(constructorParameters).ToHashSet(StringComparer.Ordinal);
    }

    private static string GetSimpleTypeName(TypeSyntax type)
    {
        var text = type.ToString();
        var separator = text.LastIndexOf(".", StringComparison.Ordinal);
        return separator < 0 ? text : text[(separator + 1)..];
    }

    private static string? FindLocalQueryType(MethodDeclarationSyntax method, string variableName)
    {
        return method.DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .Where(variable => variable.Identifier.Text.Equals(variableName, StringComparison.Ordinal))
            .Select(variable => variable.Initializer?.Value as ObjectCreationExpressionSyntax)
            .FirstOrDefault(creation => creation is not null)
            ?.Type.ToString();
    }
}