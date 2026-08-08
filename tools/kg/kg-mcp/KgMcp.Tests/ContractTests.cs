using System.Text.RegularExpressions;
using KgMcp.Core;

namespace KgMcp.Tests;

/// <summary>
/// Structural guards that need no database. These pin boundaries a behavioural test cannot see —
/// which layer owns Cypher, and what the tool surface is allowed to be.
/// </summary>
public sealed class ContractTests
{
    private static readonly string[] ExpectedTools =
    [
        "GetNodeNeighbors",
        "GetBlastRadius",
        "GetNodeDependencies",
        "GetModuleDependencies",
        "GetModuleOwnership",
        "GetActionExposure",
        "GetOrphanContracts",
        "GetJobSchedulers",
        "GetGovernedActions",
        "FindStructurallySimilarActions",
    ];

    [Fact]
    public void Server_exposes_exactly_the_ten_tier_one_tools()
    {
        var source = ReadSource("KgMcp.Server", "Tools", "KgTools.cs");

        var names = Regex.Matches(source, @"\[McpServerTool\(Name = ""(\w+)""\)")
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.Equal(ExpectedTools, names);
    }

    [Fact]
    public void No_tool_answers_an_explicitly_out_of_scope_question()
    {
        var source = ReadSource("KgMcp.Server", "Tools", "KgTools.cs");

        var toolNames = Regex.Matches(source, @"\[McpServerTool\(Name = ""(\w+)""\)")
            .Select(match => match.Groups[1].Value);

        // Guardrail 4: a structural graph cannot honestly answer these, so no tool may be named
        // as if it does. Innocent-sounding names are the failure mode being guarded against.
        var forbidden = new Regex("estimate|effort|duration|pattern|archetype|coverage|author|owner(ship)?Of|complexity", RegexOptions.IgnoreCase);
        Assert.All(toolNames, name => Assert.False(
            forbidden.IsMatch(name),
            $"Tool '{name}' is named as if it answers an out-of-scope question."));
    }

    [Fact]
    public void The_mcp_layer_holds_no_cypher_and_stays_a_delegation_shell()
    {
        var source = ReadSource("KgMcp.Server", "Tools", "KgTools.cs");

        // Traversal logic belongs in KgMcp.Core, where it is reachable by the container tests.
        var cypher = new Regex(@"\b(MATCH|OPTIONAL MATCH|RETURN|UNION|WITH\s+\w+\s+AS)\b");
        Assert.False(cypher.IsMatch(source), "Cypher found in the MCP tool layer; traversals belong in KgMcp.Core.");
    }

    [Fact]
    public void Core_contains_no_write_clause_in_any_query()
    {
        foreach (var file in Directory.EnumerateFiles(SourceRoot("KgMcp.Core"), "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);

            // Case-sensitive: Cypher keywords are uppercase in this codebase, and a case-insensitive
            // match would trip over ordinary English in doc comments.
            var writeClause = new Regex(@"\b(CREATE|MERGE|DELETE|REMOVE|DROP|SET)\b");
            Assert.False(
                writeClause.IsMatch(source),
                $"Write clause found in {Path.GetFileName(file)}; KgMcp.Core must stay read-only.");
        }
    }

    [Fact]
    public void Stdio_host_redirects_logging_to_stderr_before_the_host_is_built()
    {
        var source = ReadSource("KgMcp.Server", "Program.cs");

        var clearProviders = source.IndexOf("builder.Logging.ClearProviders", StringComparison.Ordinal);
        var addConsole = source.IndexOf("LogToStandardErrorThreshold", StringComparison.Ordinal);
        var build = source.IndexOf("builder.Build", StringComparison.Ordinal);

        Assert.True(clearProviders >= 0 && addConsole >= 0 && build >= 0);
        Assert.True(clearProviders < build, "ClearProviders must run before Build.");
        Assert.True(addConsole < build, "stderr redirection must be configured before Build.");
    }

    [Fact]
    public void Nothing_outside_tests_writes_to_stdout()
    {
        foreach (var project in new[] { "KgMcp.Core", "KgMcp.Server" })
        {
            foreach (var file in Directory.EnumerateFiles(SourceRoot(project), "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(file);

                // stdout is the MCP transport. Anything written there corrupts the JSON-RPC frame,
                // and the failure surfaces as an unusable server with no error message.
                Assert.False(
                    Regex.IsMatch(source, @"Console\.(Write|WriteLine|Out)\b"),
                    $"{Path.GetFileName(file)} writes to stdout; diagnostics must use Console.Error.");
            }
        }
    }

    [Fact]
    public void Every_id_taking_traversal_validates_its_id_before_querying()
    {
        var source = ReadSource("KgMcp.Core", "KgGraphService.cs");

        // Behavioural coverage exists per tool, but it can only see the tools that exist today.
        // This pins the rule for the next one: a public traversal that accepts an id must resolve
        // it first, so an unknown id fails loudly instead of returning an empty list.
        // Each match starts at a public async signature; the body is everything up to the next
        // member declaration, which is enough to see whether the guard is called and avoids
        // brace-counting in a regex.
        var signatures = Regex.Matches(source, @"    public async Task<[^\n]*> (?<name>\w+Async)\((?<parameters>[^)]*)\)").ToArray();

        var unguarded = signatures
            .Where(signature => Regex.IsMatch(signature.Groups["parameters"].Value, @"\bstring \w*(nodeId|moduleId|jobId|actionId|roleOrPolicyId)\b"))
            .Where(signature => signature.Groups["name"].Value != nameof(KgGraphService.ResolveLabelAsync))
            .Where(signature =>
            {
                var next = signatures.FirstOrDefault(candidate => candidate.Index > signature.Index);
                var end = next?.Index ?? source.Length;
                var body = source[signature.Index..end];
                return !body.Contains("RequireLabelAsync", StringComparison.Ordinal);
            })
            .Select(signature => signature.Groups["name"].Value)
            .ToArray();

        Assert.True(
            unguarded.Length == 0,
            $"These traversals accept an id but never resolve it, so an unknown id returns an empty list: {string.Join(", ", unguarded)}");
    }

    private static string SourceRoot(string project)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", project));
        Assert.True(Directory.Exists(root), $"Could not locate project sources at {root}");
        return root;
    }

    private static string ReadSource(params string[] relativeParts)
    {
        var path = Path.GetFullPath(Path.Combine([AppContext.BaseDirectory, "..", "..", "..", "..", .. relativeParts]));
        Assert.True(File.Exists(path), $"Could not locate source file at {path}");
        return File.ReadAllText(path);
    }
}
