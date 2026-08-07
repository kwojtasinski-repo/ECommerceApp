using KgCodegen.Core.Emit;
using KgCodegen.Core.Model;
using KgCodegen.Core.Ontology;
using KgCodegen.Core.Parsing;
using KgCodegen.Core.Spine;
using KgCodegen.Core.Validation;

namespace KgCodegen.Core.Cli;

public static class CliRunner
{
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var arguments = args.ToList();
        string? GetOption(string name)
        {
            var index = arguments.FindIndex(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 1 < arguments.Count ? arguments[index + 1] : null;
        }

        string root = GetOption("--root") ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        string ontologyPath = GetOption("--ontology") ?? Path.Combine(root, "tools", "kg", "seed", "ontology.json");
        bool check = arguments.Contains("--check", StringComparer.OrdinalIgnoreCase);

        var graph = SpineCatalog.Create();
        var resolver = new ModuleResolver();
        var symbols = DomainSymbolIndex.Build(Path.Combine(root, "ECommerceApp.Domain"));
        var applicationSymbols = DomainSymbolIndex.Build(Path.Combine(root, "ECommerceApp.Application"));
        var entity = new EntityParser(resolver).Parse(Path.Combine(root, "ECommerceApp.Infrastructure"), symbols);
        entity.Graph.MergeInto(graph);
        // Endpoint/Page must run after Action: their EXPOSED_BY edges only target Action nodes that
        // already exist, so `actions` is threaded through explicitly rather than read back off `graph`.
        var actions = new ActionParser(resolver).Parse(Path.Combine(root, "ECommerceApp.Application"));
        var messages = new MessageParser(resolver).Parse(Path.Combine(root, "ECommerceApp.Application"), actions.Graph.Nodes);
        var messageHandlers = new MessageHandlerParser(resolver).Parse(Path.Combine(root, "ECommerceApp.Application"), messages.Graph.Nodes);
        var endpoints = new EndpointParser().Parse(Path.Combine(root, "ECommerceApp.API"), applicationSymbols, actions.Graph.Nodes);
        var pages = new PageParser().Parse(Path.Combine(root, "ECommerceApp.Web"), applicationSymbols, actions.Graph.Nodes);
        var parsers = new (string Name, ParserResult Result)[]
        {
            ("Entity", entity),
            ("Repository", new RepositoryParser(resolver).Parse(Path.Combine(root, "ECommerceApp.Domain"), graph.Nodes.Where(x => x.Label == "Entity").ToList())),
            ("Action", actions),
            // Message must run after Action because its later PUBLISHES pass targets Action nodes.
            ("Message", messages),
            // MessageHandler must run after Message because HANDLED_BY targets Message nodes.
            ("MessageHandler", messageHandlers),
            ("Endpoint", endpoints),
            ("Page", pages),
            // RolePolicy must run after Endpoint/Page: GOVERNED_BY sources are their generated nodes.
            ("RolePolicy", new RolePolicyParser().Parse(
                Path.Combine(root, "ECommerceApp.Application"),
                Path.Combine(root, "ECommerceApp.API"),
                Path.Combine(root, "ECommerceApp.Web"),
                endpoints.Graph.Nodes,
                pages.Graph.Nodes))
        };

        foreach (var (indexName, index) in new[] { ("Domain", symbols), ("Application", applicationSymbols) })
        {
            foreach (var warning in index.Warnings)
            {
                stdout.WriteLine($"warning: {indexName} symbols: {warning}");
            }
        }

        foreach (var parser in parsers)
        {
            if (!ReferenceEquals(parser.Result, entity))
            {
                parser.Result.Graph.MergeInto(graph);
            }

            foreach (var warning in parser.Result.Warnings)
            {
                stdout.WriteLine($"warning: {warning}");
            }

            foreach (var warning in YieldTracker.Warnings(parser.Name, parser.Result.Graph.Nodes.Count))
            {
                stdout.WriteLine($"warning: {warning}");
            }
        }

        var report = GraphValidator.Validate(graph, OntologyLoader.Load(ontologyPath));
        foreach (var warning in report.Warnings)
        {
            stdout.WriteLine($"warning: {warning}");
        }

        foreach (var error in report.Errors)
        {
            stderr.WriteLine($"error: {error}");
        }

        foreach (var group in graph.Nodes.GroupBy(x => x.Label).OrderBy(x => x.Key))
        {
            stdout.WriteLine($"{group.Key}: {group.Count()}");
        }

        stdout.WriteLine($"Edges: {graph.Edges.Count}");
        if (report.Errors.Count > 0)
        {
            return 1;
        }

        if (!check)
        {
            var output = GetOption("--out") ?? Path.Combine(GetOption("--out-dir") ?? Path.Combine(root, "tools", "kg"), $"kg-seed.{DateTime.UtcNow:yyyyMMddHHmmss}.cypher");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output, CypherEmitter.Emit(graph, ["// Generated by KgCodegen."]));
            stdout.WriteLine($"Wrote {output}");
        }

        return 0;
    }
}
