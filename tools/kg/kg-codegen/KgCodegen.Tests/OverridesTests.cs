using KgCodegen.Core.Overrides;
using KgCodegen.Core.Model;

namespace KgCodegen.Tests;

public sealed class OverridesTests
{
    [Fact]
    public void Loader_reads_modules_and_optional_job_fields()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "overrides-sample.yaml");

        var result = OverridesLoader.Load(path);

        Assert.Equal(2, result.Modules.Count);
        Assert.Equal(new ModuleOverride("Orders", "Sales/Orders"), result.Modules[0]);
        Assert.Equal(new ModuleOverride("Payments", "Sales/Payments"), result.Modules[1]);
        Assert.Equal(2, result.Jobs.Count);
        Assert.Equal("RefreshTokenCleanup", result.Jobs[0].TaskName);
        Assert.Equal("0 0 * * *", result.Jobs[0].CronExpression);
        Assert.Null(result.Jobs[0].TimeZoneId);
        Assert.Null(result.Jobs[0].TriggerMode);
        Assert.Equal("Europe/Warsaw", result.Jobs[1].TimeZoneId);
        Assert.Equal("Scheduled", result.Jobs[1].TriggerMode);
    }

    [Fact]
    public void Loader_missing_file_names_absolute_path_and_override_option()
    {
        var path = Path.Combine(Path.GetTempPath(), "missing-overrides-" + Guid.NewGuid().ToString("N"), "overrides.yaml");

        var exception = Assert.Throws<FileNotFoundException>(() => OverridesLoader.Load(path));

        Assert.Contains(Path.GetFullPath(path), exception.Message, StringComparison.Ordinal);
        Assert.Contains("--overrides", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Applier_adds_supplied_properties_without_overwriting_parser_mode()
    {
        var graph = Graph.Empty();
        graph.Nodes.Add(new CypherNode("Job", "Demo.RefreshJob", new Dictionary<string, object?>
        {
            ["taskName"] = "RefreshTokenCleanup",
            ["triggerMode"] = "Deferred"
        }));

        var warnings = JobOverrideApplier.Apply(graph, [new JobOverride("RefreshTokenCleanup", "0 0 * * *", "UTC", null)]);

        Assert.Empty(warnings);
        var properties = graph.Nodes[0].Properties;
        Assert.Equal("0 0 * * *", properties["cronExpression"]);
        Assert.Equal("UTC", properties["timeZoneId"]);
        Assert.Equal("Deferred", properties["triggerMode"]);
    }

    [Fact]
    public void Applier_leaves_unmatched_job_without_new_properties_and_warns_once()
    {
        var graph = Graph.Empty();
        graph.Nodes.Add(new CypherNode("Job", "Demo.RefreshJob", new Dictionary<string, object?>
        {
            ["taskName"] = "RefreshTokenCleanup",
            ["triggerMode"] = null
        }));

        var warnings = JobOverrideApplier.Apply(graph, [new JobOverride("MissingTask", "0 0 * * *", "UTC", "Scheduled")]);

        var warning = Assert.Single(warnings);
        Assert.Contains("MissingTask", warning, StringComparison.Ordinal);
        Assert.DoesNotContain("cronExpression", graph.Nodes[0].Properties.Keys);
        Assert.DoesNotContain("timeZoneId", graph.Nodes[0].Properties.Keys);
    }

    [Fact]
    public void Applier_does_not_mutate_ambiguous_task_name_and_warns_once()
    {
        var graph = Graph.Empty();
        // Distinct dictionary instances on purpose: sharing one would let an in-place mutation
        // of a single node look like it had touched both, hiding the bug this test pins.
        graph.Nodes.Add(new CypherNode("Job", "Demo.FirstJob", new Dictionary<string, object?> { ["taskName"] = "DuplicateTask", ["triggerMode"] = null }));
        graph.Nodes.Add(new CypherNode("Job", "Demo.SecondJob", new Dictionary<string, object?> { ["taskName"] = "DuplicateTask", ["triggerMode"] = null }));

        var warnings = JobOverrideApplier.Apply(graph, [new JobOverride("DuplicateTask", "0 0 * * *", "UTC", "Scheduled")]);

        var warning = Assert.Single(warnings);
        Assert.Contains("ambiguous", warning, StringComparison.OrdinalIgnoreCase);
        Assert.All(graph.Nodes, node => Assert.DoesNotContain("cronExpression", node.Properties.Keys));
        Assert.All(graph.Nodes, node => Assert.DoesNotContain("timeZoneId", node.Properties.Keys));
    }

    [Fact]
    public void Loader_rejects_misspelled_key_instead_of_dropping_the_override()
    {
        var path = Path.Combine(Path.GetTempPath(), "overrides-typo-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(path, "modules: []\njobs:\n  - taskName: RefreshTokenCleanup\n    cronExpresion: \"0 0 * * *\"\n");

        try
        {
            var exception = Assert.Throws<InvalidDataException>(() => OverridesLoader.Load(path));

            Assert.Contains(path, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }
}