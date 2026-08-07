using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using KgCodegen.Core.Model;

namespace KgCodegen.Core.Parsing;

public sealed class MessageParser(ModuleResolver modules)
{
    public ParserResult Parse(string applicationRoot, IReadOnlyList<CypherNode> actions)
    {
        _ = modules;
        _ = actions;
        var graph = Graph.Empty();
        var warnings = new List<string>();
        var messageFiles = Directory.EnumerateFiles(
                applicationRoot,
                "*.cs",
                SearchOption.AllDirectories)
            .Where(file => Path.GetDirectoryName(file)?.EndsWith("Messages", StringComparison.Ordinal) == true)
            .ToArray();

        var messageSyntax = new List<(string Path, CompilationUnitSyntax Root, TypeDeclarationSyntax Type)>();
        foreach (var file in messageFiles)
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetCompilationUnitRoot();
            foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (type.BaseList?.Types.Any(baseType => baseType.Type.ToString().Equals("IMessage", StringComparison.Ordinal)) == true)
                {
                    messageSyntax.Add((file, root, type));
                }
            }
        }

        var messages = messageSyntax
            .Select(item => new CypherNode("Message", GetFullyQualifiedName(item.Root, item.Type), new Dictionary<string, object?>()))
            .ToArray();
        var resolver = new MessageNameResolver(messages);
        var registry = MessageTypeRegistryIndex.Build(applicationRoot, resolver, messages);
        warnings.AddRange(registry.Warnings);

        foreach (var message in messages)
        {
            var key = registry.KeyFor(message.Id);
            if (key is null)
            {
                warnings.Add($"Message '{message.Id}' is not registered in MessageTypeRegistry.");
            }

            graph.Nodes.Add(message with { Properties = new Dictionary<string, object?> { ["key"] = key } });
        }

        var actionIds = actions
            .Where(action => action.Label.Equals("Action", StringComparison.Ordinal))
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        ParsePublishes(applicationRoot, messageSyntax, messages, actionIds, resolver, graph, warnings);

        return new ParserResult(graph, warnings);
    }

    private static void ParsePublishes(
        string applicationRoot,
        IReadOnlyList<(string Path, CompilationUnitSyntax Root, TypeDeclarationSyntax Type)> messageSyntax,
        IReadOnlyList<CypherNode> messages,
        IReadOnlySet<string> actionIds,
        MessageNameResolver resolver,
        Graph graph,
        List<string> warnings)
    {
        var messageFiles = messageSyntax.Select(item => item.Path).ToHashSet(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(applicationRoot, "*Service.cs", SearchOption.AllDirectories))
        {
            if (messageFiles.Contains(file))
            {
                continue;
            }

            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetCompilationUnitRoot();
            var namespaceName = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? "";
            foreach (var service in root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(type => type.Identifier.Text.EndsWith("Service", StringComparison.Ordinal)))
            {
                var classId = string.IsNullOrEmpty(namespaceName) ? service.Identifier.Text : $"{namespaceName}.{service.Identifier.Text}";
                foreach (var method in service.Members.OfType<MethodDeclarationSyntax>().Where(method => method.Modifiers.Any(modifier => modifier.RawKind == (int)SyntaxKind.PublicKeyword)))
                {
                    var actionId = $"{classId}.{method.Identifier.Text}";
                    if (!actionIds.Contains(actionId))
                    {
                        continue;
                    }

                    foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
                    {
                        var methodName = invocation.Expression switch
                        {
                            IdentifierNameSyntax identifier => identifier.Identifier.Text,
                            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
                            _ => ""
                        };
                        if (!methodName.Equals("EnqueueAsync", StringComparison.Ordinal) && !methodName.Equals("PublishAsync", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var argument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
                        var messageName = argument switch
                        {
                            ObjectCreationExpressionSyntax creation => creation.Type.ToString(),
                            IdentifierNameSyntax identifier => FindLocalMessageType(method, identifier.Identifier.Text),
                            _ => null
                        };
                        if (messageName is null)
                        {
                            warnings.Add($"Could not extract published message in {actionId}: {argument ?? invocation}.");
                            continue;
                        }

                        var resolved = resolver.Resolve(messageName, root, out var warning);
                        if (warning is not null)
                        {
                            warnings.Add(warning);
                        }

                        if (resolved is null || !messages.Any(message => message.Id.Equals(resolved, StringComparison.Ordinal)))
                        {
                            continue;
                        }

                        var edge = new CypherEdge("PUBLISHES", "Action", actionId, "Message", resolved);
                        if (!graph.Edges.Contains(edge))
                        {
                            graph.Edges.Add(edge);
                        }
                    }
                }
            }
        }
    }

    private static string? FindLocalMessageType(MethodDeclarationSyntax method, string variableName)
    {
        return method.DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .Where(variable => variable.Identifier.Text.Equals(variableName, StringComparison.Ordinal))
            .Select(variable => variable.Initializer?.Value as ObjectCreationExpressionSyntax)
            .FirstOrDefault(creation => creation is not null)
            ?.Type.ToString();
    }

    private static string GetFullyQualifiedName(CompilationUnitSyntax root, TypeDeclarationSyntax type)
    {
        var namespaceName = root.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault(namespaceDeclaration => namespaceDeclaration.Span.Contains(type.SpanStart))
            ?.Name.ToString();
        return string.IsNullOrEmpty(namespaceName) ? type.Identifier.Text : $"{namespaceName}.{type.Identifier.Text}";
    }
}