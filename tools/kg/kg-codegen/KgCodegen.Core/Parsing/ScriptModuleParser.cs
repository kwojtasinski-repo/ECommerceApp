using System.Text.RegularExpressions;
using KgCodegen.Core.Model;

namespace KgCodegen.Core.Parsing;

public sealed class ScriptModuleParser
{
    private static readonly Regex DefinePattern = new(
        "^define\\s*\\(\\s*\\[(?<deps>[^\\]]*)\\]",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly Regex DependencyPattern = new(
        "'([^']*)'|\"([^\"]*)\"",
        RegexOptions.CultureInvariant);

    private static readonly Regex RequirePattern = new(
        "require\\s*\\(\\s*\\[(?<deps>[^\\]]*)\\]",
        RegexOptions.CultureInvariant);

    private static readonly Regex UrlPattern = new(
        "(?:fetch|ajaxRequest\\.send)\\s*\\(\\s*[\"'](?<url>/[^\"']*)",
        RegexOptions.CultureInvariant);

    private static readonly Regex PageIdPattern = new(
        "^(?<namespace>.+)\\.(?<controller>[^.]+Controller)\\.(?<method>[^.]+)$",
        RegexOptions.CultureInvariant);

    public ParserResult Parse(
        string webRoot,
        IReadOnlyList<CypherNode> pages,
        IReadOnlyList<CypherNode> endpoints)
    {
        var graph = Graph.Empty();
        var warnings = new List<string>();
        var jsRoot = Path.Combine(webRoot, "wwwroot", "js");
        if (!Directory.Exists(jsRoot))
        {
            return new ParserResult(graph, warnings);
        }

        var modules = Directory.EnumerateFiles(jsRoot, "*.js", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => ReadModule(jsRoot, path))
            .Where(module => module is not null)
            .Select(module => module!)
            .ToArray();
        var moduleIds = modules.Select(module => module.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var module in modules)
        {
            graph.Nodes.Add(new CypherNode("ScriptModule", module.Id, new Dictionary<string, object?>()));
            graph.Edges.Add(new CypherEdge("CONTAINS", "Host", "WebHost", "ScriptModule", module.Id));
        }

        foreach (var module in modules)
        {
            foreach (var dependency in ExtractDependencies(module.Dependencies))
            {
                if (!moduleIds.Contains(dependency))
                {
                    warnings.Add($"ScriptModule '{module.Id}' depends on unknown module '{dependency}'.");
                    continue;
                }

                graph.Edges.Add(new CypherEdge("DEPENDS_ON", "ScriptModule", module.Id, "ScriptModule", dependency));
            }
        }

        var knownModules = moduleIds;
        var webFiles = Directory.EnumerateFiles(webRoot, "*.cshtml", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).StartsWith("_", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal);
        foreach (var file in webFiles)
        {
            var content = File.ReadAllText(file);
            var requireMatches = RequirePattern.Matches(content);
            var urlMatches = UrlPattern.Matches(content);
            if (requireMatches.Count == 0 && urlMatches.Count == 0)
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(webRoot, file);
            var hasViewShape = TryGetViewShape(webRoot, file, out var view);
            var matchingPages = hasViewShape
                ? pages.Where(page => page.Label == "Page" && MatchesPage(page, view)).ToArray()
                : [];

            // A view that resolves to no `Page` node is only worth a warning once something in it
            // would actually have become an edge. Razor view files outnumber controller actions —
            // `Areas/Catalog/Views/Product/{Add,Edit}ItemNew.cshtml` are rendered by `Create`/`Edit`
            // via `return View("AddItemNew", …)`, so no `Page` id can ever match their filename — and
            // both contain only same-host MVC `fetch(...)` calls, which fact #5 requires to be silent.
            // Warning on the resolution itself would report those two as failures every run and bury
            // the Guardrail-5 signal under noise.
            var blockedBySource = false;

            foreach (Match require in requireMatches)
            {
                foreach (var dependency in ExtractDependencies(require.Groups["deps"].Value))
                {
                    if (!knownModules.Contains(dependency))
                    {
                        warnings.Add($"Page view '{relativePath}' uses unknown ScriptModule '{dependency}'.");
                        continue;
                    }

                    if (matchingPages.Length == 0)
                    {
                        blockedBySource = true;
                        continue;
                    }

                    foreach (var page in matchingPages)
                    {
                        graph.Edges.Add(new CypherEdge("USES", "Page", page.Id, "ScriptModule", dependency));
                    }
                }
            }

            foreach (Match urlMatch in urlMatches)
            {
                var url = urlMatch.Groups["url"].Value;
                var endpoint = endpoints.FirstOrDefault(candidate =>
                    candidate.Label == "Endpoint" && EndpointMatches(candidate, url));
                if (endpoint is null)
                {
                    if (url.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                    {
                        warnings.Add($"Could not resolve API URL '{url}' from Razor view '{relativePath}'.");
                    }

                    continue;
                }

                if (matchingPages.Length == 0)
                {
                    blockedBySource = true;
                    continue;
                }

                foreach (var page in matchingPages)
                {
                    graph.Edges.Add(new CypherEdge("USES", "Page", page.Id, "Endpoint", endpoint.Id));
                }
            }

            if (blockedBySource)
            {
                warnings.Add(hasViewShape
                    ? $"Could not resolve Razor view '{relativePath}' to a Page node."
                    : $"Could not map Razor view '{relativePath}' to a Page.");
            }
        }

        return new ParserResult(graph, warnings);
    }

    private static ModuleDeclaration? ReadModule(string jsRoot, string path)
    {
        var content = File.ReadAllText(path).TrimStart('\uFEFF');
        var match = DefinePattern.Match(content);
        if (!match.Success)
        {
            return null;
        }

        var id = Path.GetRelativePath(jsRoot, path)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        id = id.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ? id[..^3] : id;
        return new ModuleDeclaration(id, match.Groups["deps"].Value);
    }

    private static IEnumerable<string> ExtractDependencies(string text)
    {
        foreach (Match match in DependencyPattern.Matches(text))
        {
            var dependency = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (!string.IsNullOrWhiteSpace(dependency))
            {
                yield return dependency.Trim();
            }
        }
    }

    private static bool TryGetViewShape(string webRoot, string file, out ViewShape view)
    {
        var relative = Path.GetRelativePath(webRoot, file)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        var parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 5 && parts[0].Equals("Areas", StringComparison.Ordinal) && parts[2].Equals("Views", StringComparison.Ordinal))
        {
            view = new ViewShape(parts[1], parts[3], Path.GetFileNameWithoutExtension(parts[4]));
            return true;
        }

        if (parts.Length == 3 && parts[0].Equals("Views", StringComparison.Ordinal))
        {
            view = new ViewShape(null, parts[1], Path.GetFileNameWithoutExtension(parts[2]));
            return true;
        }

        view = default;
        return false;
    }

    private static bool MatchesPage(CypherNode page, ViewShape view)
    {
        var match = PageIdPattern.Match(page.Id);
        if (!match.Success)
        {
            return false;
        }

        var method = Regex.Replace(match.Groups["method"].Value, "#\\d+$", string.Empty, RegexOptions.CultureInvariant);
        var namespaceName = match.Groups["namespace"].Value;
        var areaMatch = Regex.Match(namespaceName, "\\.Areas\\.(?<area>[^.]+)\\.Controllers$", RegexOptions.CultureInvariant);
        var area = areaMatch.Success ? areaMatch.Groups["area"].Value : null;
        var controller = match.Groups["controller"].Value[..^"Controller".Length];
        return string.Equals(area, view.Area, StringComparison.Ordinal) &&
               string.Equals(controller, view.Controller, StringComparison.Ordinal) &&
               string.Equals(method, view.Method, StringComparison.Ordinal);
    }

    private static bool EndpointMatches(CypherNode endpoint, string url)
    {
        if (!endpoint.Properties.TryGetValue("route", out var routeValue) || routeValue is not string route || string.IsNullOrWhiteSpace(route))
        {
            return false;
        }

        var normalizedUrl = url.Trim('/');
        var normalizedRoute = route.Trim('/');
        if (normalizedUrl.Equals(normalizedRoute, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var templateStart = normalizedRoute.IndexOf('{', StringComparison.Ordinal);
        return templateStart >= 0 &&
               normalizedUrl.StartsWith(normalizedRoute[..templateStart].TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ModuleDeclaration(string Id, string Dependencies);
    private readonly record struct ViewShape(string? Area, string Controller, string Method);
}
