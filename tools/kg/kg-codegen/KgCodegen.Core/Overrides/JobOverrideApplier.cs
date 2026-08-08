using KgCodegen.Core.Model;

namespace KgCodegen.Core.Overrides;

public static class JobOverrideApplier
{
    public static IReadOnlyList<string> Apply(Graph graph, IReadOnlyList<JobOverride> overrides)
    {
        var warnings = new List<string>();
        var jobs = graph.Nodes.Where(node => node.Label == "Job").ToArray();

        foreach (var jobOverride in overrides)
        {
            var matches = jobs
                .Where(job => job.Properties.TryGetValue("taskName", out var taskName)
                    && taskName is string value
                    && string.Equals(value, jobOverride.TaskName, StringComparison.Ordinal))
                .ToArray();

            if (matches.Length == 0)
            {
                warnings.Add($"Stale job override for taskName '{jobOverride.TaskName}' matched no Job node.");
                continue;
            }

            if (matches.Length > 1)
            {
                warnings.Add($"Ambiguous job override for taskName '{jobOverride.TaskName}' matched {matches.Length} Job nodes; no mutation applied.");
                continue;
            }

            var job = matches[0];
            var properties = new Dictionary<string, object?>(job.Properties);
            if (jobOverride.CronExpression is not null)
            {
                properties["cronExpression"] = jobOverride.CronExpression;
            }

            if (jobOverride.TimeZoneId is not null)
            {
                properties["timeZoneId"] = jobOverride.TimeZoneId;
            }

            if (jobOverride.TriggerMode is not null)
            {
                properties["triggerMode"] = jobOverride.TriggerMode;
            }

            graph.Nodes[graph.Nodes.IndexOf(job)] = job with { Properties = properties };
        }

        return warnings;
    }
}