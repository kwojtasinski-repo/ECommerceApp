namespace KgCodegen.Core.Parsing;

public sealed class ModuleResolver
{
    private readonly IReadOnlyDictionary<string, string> paths;

    public ModuleResolver(IReadOnlyDictionary<string, string> paths) => this.paths = paths;

    public string? Resolve(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').Trim('/');
        return paths.Where(x => normalized.StartsWith(x.Value.Trim('/') + '/', StringComparison.OrdinalIgnoreCase)
                       || normalized.Equals(x.Value.Trim('/'), StringComparison.OrdinalIgnoreCase)
                       || normalized.Contains('/' + x.Value.Trim('/') + '/', StringComparison.OrdinalIgnoreCase)
                               || normalized.EndsWith('/' + x.Value.Trim('/'), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Value.Length)
            .Select(x => x.Key)
            .FirstOrDefault();
    }
}