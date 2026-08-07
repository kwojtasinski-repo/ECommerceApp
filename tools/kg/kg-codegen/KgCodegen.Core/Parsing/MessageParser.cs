using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using KgCodegen.Core.Model;

namespace KgCodegen.Core.Parsing;

/// <summary>
/// Emits `Message` nodes (types implementing `IMessage`) and `Action-[:PUBLISHES]->Message` edges.
///
/// `Message` nodes are deliberately un-contained: no `Module-[:CONTAINS]->Message` edge is emitted,
/// matching how `Role`/`Policy` are treated. A message is a contract between modules, not a member
/// of one.
///
/// Three deliberate limits, all of which under-report rather than guess:
///
/// 1. **Publishes are read from `*Service.cs` only**, because `PUBLISHES` must originate at an
///    `Action` node and only service methods become actions. Three real publish sites therefore
///    produce no edge: `Inventory/Availability/Handlers/ShipmentDeliveredHandler.cs`,
///    `ShipmentFailedHandler.cs` and `ShipmentPartiallyDeliveredHandler.cs` all enqueue
///    `StockReconciliationRequired`. The ontology has no `MessageHandler-[:PUBLISHES]->Message`
///    triple, so the edge is not merely unemitted here — it is currently unrepresentable. Adding
///    that triple is a candidate ontology amendment; until it lands, this gap is silent by design
///    rather than warned about, because the parser is doing exactly what the ontology allows.
///    Publishes from `*Job.cs` are the same case and are Phase 4c's concern.
/// 2. **An `IMessage` type that `MessageTypeRegistry` does not register still gets a node**, with
///    `key: null` and a warning, and keeps its `HANDLED_BY` edges. Six real messages are in this
///    state. Dropping them would hide handlers that genuinely run — the missing registry entry is
///    reported as a fact about the code, not papered over by omitting the node.
/// 3. **`PublishAsync` is accepted alongside `EnqueueAsync` but is currently dead**: the repo has
///    exactly two `PublishAsync` occurrences, both interface declarations in `Messaging/`, and no
///    call sites. The branch exists so a future call site is not silently skipped; it has never
///    been exercised against real code.
/// </summary>
public sealed class MessageParser
{
    public ParserResult Parse(string applicationRoot, IReadOnlyList<CypherNode> actions)
    {
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
            .Select(item => new CypherNode("Message", SyntaxNaming.FullyQualifiedName(item.Root, item.Type), new Dictionary<string, object?>()))
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
        foreach (var file in Directory.EnumerateFiles(applicationRoot, ServiceActionSites.ServiceFilePattern, SearchOption.AllDirectories))
        {
            if (messageFiles.Contains(file))
            {
                continue;
            }

            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetCompilationUnitRoot();
            foreach (var site in ServiceActionSites.Enumerate(root))
            {
                // The id has to be one `ActionParser` actually produced; a publish from a method
                // that is not an action has nowhere to originate.
                if (!actionIds.Contains(site.ActionId))
                {
                    continue;
                }

                foreach (var invocation in site.Method.DescendantNodes().OfType<InvocationExpressionSyntax>())
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

                    // Only the enqueued argument counts. Scanning the whole method body for
                    // `new SomeMessage(...)` would turn every constructed value — including
                    // non-message types such as `RefundApprovedItem` — into a publish.
                    var argument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
                    var messageName = argument switch
                    {
                        ObjectCreationExpressionSyntax creation => creation.Type.ToString(),
                        IdentifierNameSyntax identifier => FindLocalMessageType(site.Method, identifier.Identifier.Text),
                        _ => null
                    };
                    if (messageName is null)
                    {
                        warnings.Add($"Could not extract published message in {site.ActionId}: {argument ?? invocation}.");
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

                    var edge = new CypherEdge("PUBLISHES", "Action", site.ActionId, "Message", resolved);
                    if (!graph.Edges.Contains(edge))
                    {
                        graph.Edges.Add(edge);
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
}