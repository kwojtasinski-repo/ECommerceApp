using System.Text.RegularExpressions;

namespace KgMcp.Tests;

public sealed class ContractTests
{
    [Fact]
    public void Server_exposes_exactly_ten_tier_one_tools()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "KgMcp.Server", "Tools", "KgTools.cs"));
        var names = Regex.Matches(source, @"\[McpServerTool[^\]]*\][\s\S]*?Task<string>\s+(\w+)")
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.Equal(10, names.Length);
        Assert.Equal(new[] { "GetNodeNeighbors", "GetBlastRadius", "GetNodeDependencies", "GetModuleDependencies", "GetModuleOwnership", "GetActionExposure", "GetOrphanContracts", "GetJobSchedulers", "GetGovernedActions", "FindStructurallySimilarActions" }, names);
        Assert.Equal(10, Regex.Matches(source, @"Name\s*=\s*""(GetNodeNeighbors|GetBlastRadius|GetNodeDependencies|GetModuleDependencies|GetModuleOwnership|GetActionExposure|GetOrphanContracts|GetJobSchedulers|GetGovernedActions|FindStructurallySimilarActions)""").Count);
    }

    [Fact]
    public void Server_queries_contain_no_write_clauses()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "KgMcp.Server", "Tools", "KgTools.cs"));
        Assert.DoesNotMatch(new Regex(@"\b(CREATE|MERGE|SET|DELETE|REMOVE|DROP)\b", RegexOptions.IgnoreCase), source);
    }

    [Fact]
    public void Blast_radius_clamp_is_present_and_structural_similarity_is_calibrated()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "KgMcp.Server", "Tools", "KgTools.cs"));
        Assert.Contains("Math.Clamp(maxDepth, 1, 5)", source);
        Assert.Contains("heuristic structural proxy", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Stdio_host_redirects_logging_before_build()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "KgMcp.Server", "Program.cs"));
        Assert.True(source.IndexOf("builder.Logging.ClearProviders", StringComparison.Ordinal) < source.IndexOf("var app = builder.Build", StringComparison.Ordinal));
        Assert.Contains("LogToStandardErrorThreshold", source, StringComparison.Ordinal);
        Assert.Contains("migrations=none", source, StringComparison.Ordinal);
        Assert.Contains("graph-load=external", source, StringComparison.Ordinal);
    }
}
