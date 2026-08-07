namespace KgCodegen.Core.Parsing;

public static class YieldTracker
{
    public static IReadOnlyList<string> Warnings(string parserName, int count) =>
        count == 0 ? [$"Parser '{parserName}' yielded zero nodes."] : [];
}