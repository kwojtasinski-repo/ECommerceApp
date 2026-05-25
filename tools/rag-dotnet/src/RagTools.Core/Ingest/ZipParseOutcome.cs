namespace RagTools.Core.Ingest;

/// <summary>
/// Lightweight descriptor for a ZIP entry — name + length only, no stream. Lets
/// <see cref="BatchValidator"/> apply name/size policy without touching content.
/// </summary>
public readonly record struct ZipEntryInfo(string Name, long Length);

/// <summary>
/// Successfully parsed ZIP payload — ready to feed into <see cref="IBatchIngestService.Enqueue"/>.
/// </summary>
public sealed record ParsedBatch(
    IReadOnlyList<BatchDocument> Documents,
    MetadataRulesSection? BatchRules,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Discriminated outcome of <see cref="IZipBatchParser.ParseAsync"/>.
/// Failure cases reuse <see cref="BatchIngestError"/> so HTTP/CLI mappers handle one enum.
/// </summary>
public abstract record ZipParseOutcome
{
    private ZipParseOutcome() { }

    public sealed record Success(ParsedBatch Batch) : ZipParseOutcome;

    public sealed record Failure(
        BatchIngestError Error,
        string Message,
        IReadOnlyDictionary<string, object?>? Details = null) : ZipParseOutcome;
}

/// <summary>
/// Outcome of <see cref="BatchValidator.Validate"/> — either every rule passed or one failed.
/// </summary>
public abstract record ValidationOutcome
{
    private ValidationOutcome() { }

    /// <summary>
    /// All rules passed. Contains the rules parsed from <c>metadata-rules.yaml</c>
    /// (null if YAML parsing failed non-fatally), the subset of entries eligible
    /// for ingestion (after extension / zero-byte / config-file filtering), and
    /// any non-fatal warnings to surface to the caller.
    /// </summary>
    public sealed record Ok(
        MetadataRulesSection? Rules,
        IReadOnlyList<ZipEntryInfo> EligibleDocs,
        IReadOnlyList<string> Warnings) : ValidationOutcome;

    /// <summary>One rule failed. Wraps a <see cref="ZipParseOutcome.Failure"/> verbatim
    /// so the parser can return it without re-wrapping.</summary>
    public sealed record Bad(ZipParseOutcome.Failure Failure) : ValidationOutcome;
}

/// <summary>
/// Pure parser/validator for ingest ZIPs. Consumes the request body stream, returns either
/// a <see cref="ParsedBatch"/> or a typed <see cref="ZipParseOutcome.Failure"/>. No HTTP, no I/O
/// beyond writing the body to a temp file and reading entries — testable with in-memory ZIPs.
/// </summary>
public interface IZipBatchParser
{
    Task<ZipParseOutcome> ParseAsync(Stream zipStream, CancellationToken ct = default);
}
