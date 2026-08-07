using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using KgCodegen.Core.Model;

namespace KgCodegen.Core.Parsing;

/// <summary>
/// Emits `MessageHandler` nodes and `Message-[:HANDLED_BY]->MessageHandler` edges.
///
/// A handler class is counted once no matter how many `IMessageHandler&lt;T&gt;` interfaces it
/// declares — `ProductCacheInvalidationHandler` implements four and is one node with four in-edges.
/// Taking only the first interface would silently halve the graph's view of what runs on a message.
/// </summary>
public sealed class MessageHandlerParser(ModuleResolver modules)
{
    public ParserResult Parse(string applicationRoot, IReadOnlyList<CypherNode> messages)
    {
        var graph = Graph.Empty();
        var warnings = new List<string>();
        var resolver = new MessageNameResolver(messages);
        var messageIds = messages
            .Where(message => message.Label.Equals("Message", StringComparison.Ordinal))
            .Select(message => message.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(applicationRoot, "*Handler.cs", SearchOption.AllDirectories))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetCompilationUnitRoot();
            var module = modules.Resolve(Path.GetRelativePath(applicationRoot, file));
            if (module is null)
            {
                continue;
            }

            foreach (var handler in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var markers = handler.BaseList?.Types
                    .Select(baseType => baseType.Type)
                    .OfType<GenericNameSyntax>()
                    .Where(type => type.Identifier.Text.Equals("IMessageHandler", StringComparison.Ordinal) ||
                                   type.Identifier.Text.Equals("IIdAwareMessageHandler", StringComparison.Ordinal))
                    .ToArray() ?? [];
                if (markers.Length == 0)
                {
                    continue;
                }

                var handlerId = SyntaxNaming.FullyQualifiedName(root, handler);
                var idAware = markers.Any(marker => marker.Identifier.Text.Equals("IIdAwareMessageHandler", StringComparison.Ordinal));
                graph.Nodes.Add(new CypherNode("MessageHandler", handlerId, new Dictionary<string, object?> { ["idAware"] = idAware }));
                graph.Edges.Add(new CypherEdge("CONTAINS", "Module", module, "MessageHandler", handlerId));

                foreach (var marker in markers)
                {
                    var messageName = marker.TypeArgumentList.Arguments.FirstOrDefault()?.ToString();
                    if (messageName is null)
                    {
                        warnings.Add($"Could not resolve message type for handler {handlerId} in {file}.");
                        continue;
                    }

                    // One unresolved type argument produces one warning: the resolver's own message
                    // already says why it could not be resolved, so a second generic one on top of
                    // it would read as two separate problems.
                    var resolved = resolver.Resolve(messageName, root, out var warning);
                    if (resolved is null || !messageIds.Contains(resolved))
                    {
                        warnings.Add(warning ?? $"Could not resolve handled message '{messageName}' for {handlerId} in {file}.");
                        continue;
                    }

                    if (warning is not null)
                    {
                        warnings.Add(warning);
                    }

                    graph.Edges.Add(new CypherEdge("HANDLED_BY", "Message", resolved, "MessageHandler", handlerId));
                }
            }
        }

        return new ParserResult(graph, warnings);
    }
}