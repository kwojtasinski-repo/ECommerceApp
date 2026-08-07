namespace KgCodegen.Core.Validation;

public sealed record ValidationReport(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);