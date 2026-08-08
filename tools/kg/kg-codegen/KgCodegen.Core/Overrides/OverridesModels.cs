namespace KgCodegen.Core.Overrides;

public sealed record ModuleOverride(string Id, string Path);

public sealed record JobOverride(string TaskName, string? CronExpression, string? TimeZoneId, string? TriggerMode);

public sealed record OverridesData(IReadOnlyList<ModuleOverride> Modules, IReadOnlyList<JobOverride> Jobs);