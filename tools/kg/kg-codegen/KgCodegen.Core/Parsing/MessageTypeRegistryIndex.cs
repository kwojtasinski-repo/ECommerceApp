using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using KgCodegen.Core.Model;

namespace KgCodegen.Core.Parsing;

internal sealed class MessageTypeRegistryIndex
{
    private readonly IReadOnlyDictionary<string, string> keys;

    private MessageTypeRegistryIndex(IReadOnlyDictionary<string, string> keys, IReadOnlyList<string> warnings)
    {
        this.keys = keys;
        Warnings = warnings;
    }

    internal IReadOnlyList<string> Warnings { get; }

    internal int Count => keys.Count;

    internal static MessageTypeRegistryIndex Build(string applicationRoot, MessageNameResolver resolver, IReadOnlyList<CypherNode> messages)
    {
        var warnings = new List<string>();
        var path = Path.Combine(applicationRoot, "Messaging", "MessageTypeRegistry.cs");
        if (!File.Exists(path))
        {
            warnings.Add($"MessageTypeRegistry.cs was not found at {path}.");
            return new MessageTypeRegistryIndex(new Dictionary<string, string>(StringComparer.Ordinal), warnings);
        }

        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path).GetCompilationUnitRoot();
        var registrations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => invocation.Expression.ToString().Equals("Register", StringComparison.Ordinal))
            .ToArray();
        if (registrations.Length == 0)
        {
            warnings.Add($"MessageTypeRegistry.cs contains no Register calls.");
            return new MessageTypeRegistryIndex(new Dictionary<string, string>(StringComparer.Ordinal), warnings);
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var registration in registrations)
        {
            var arguments = registration.ArgumentList.Arguments;
            if (arguments.Count < 2)
            {
                warnings.Add($"Could not parse MessageTypeRegistry registration in {path}: {registration}.");
                continue;
            }

            if (arguments[0].Expression is not TypeOfExpressionSyntax typeOf ||
                arguments[1].Expression is not LiteralExpressionSyntax keyLiteral ||
                !keyLiteral.IsKind(SyntaxKind.StringLiteralExpression))
            {
                warnings.Add($"Could not parse MessageTypeRegistry registration in {path}: {registration}.");
                continue;
            }

            var resolved = resolver.Resolve(typeOf.Type.ToString(), root, out var warning);
            if (warning is not null)
            {
                warnings.Add(warning);
            }

            if (resolved is not null && messages.Any(message => message.Id.Equals(resolved, StringComparison.Ordinal)))
            {
                map[resolved] = keyLiteral.Token.ValueText;
            }
        }

        return new MessageTypeRegistryIndex(map, warnings);
    }

    internal string? KeyFor(string messageFqcn) => keys.TryGetValue(messageFqcn, out var key) ? key : null;
}