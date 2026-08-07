using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KgCodegen.Core.Parsing;

internal static class OutboxPublishResolver
{
    internal static string? ResolvePublishedMessage(
        InvocationExpressionSyntax invocation,
        MethodDeclarationSyntax method,
        string sourceId,
        MessageNameResolver resolver,
        CompilationUnitSyntax file,
        out string? warning)
    {
        warning = null;
        var argument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        var messageName = argument switch
        {
            ObjectCreationExpressionSyntax creation => creation.Type.ToString(),
            IdentifierNameSyntax identifier => FindLocalMessageType(method, identifier.Identifier.Text),
            _ => null
        };
        if (messageName is null)
        {
            warning = $"Could not extract published message in {sourceId}: {argument ?? invocation}.";
            return null;
        }

        return resolver.Resolve(messageName, file, out warning);
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