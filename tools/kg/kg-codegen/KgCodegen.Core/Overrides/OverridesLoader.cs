using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KgCodegen.Core.Overrides;

public static class OverridesLoader
{
    public static OverridesData Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Overrides file was not found at '{fullPath}'. Pass --overrides to use another file.",
                fullPath);
        }

        try
        {
            var yaml = File.ReadAllText(fullPath);
            var document = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build()
                .Deserialize<OverridesDocument>(yaml) ?? new OverridesDocument();

            return new OverridesData(
                (document.Modules ?? []).Select(module => new ModuleOverride(module.Id ?? string.Empty, module.Path ?? string.Empty)).ToArray(),
                (document.Jobs ?? []).Select(job => new JobOverride(
                    job.TaskName ?? string.Empty,
                    job.CronExpression,
                    job.TimeZoneId,
                    job.TriggerMode)).ToArray());
        }
        catch (Exception exception) when (exception is not FileNotFoundException)
        {
            throw new InvalidDataException($"Could not parse overrides file '{fullPath}'.", exception);
        }
    }

    private sealed class OverridesDocument
    {
        public List<ModuleDocument>? Modules { get; init; }

        public List<JobDocument>? Jobs { get; init; }
    }

    private sealed class ModuleDocument
    {
        public string? Id { get; init; }

        public string? Path { get; init; }
    }

    private sealed class JobDocument
    {
        public string? TaskName { get; init; }

        public string? CronExpression { get; init; }

        public string? TimeZoneId { get; init; }

        public string? TriggerMode { get; init; }
    }
}