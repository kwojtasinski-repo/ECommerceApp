namespace RagTools.Core.Ingest;

/// <summary>
/// Exhaustive list of expected ingest failure modes.
/// Mapped to HTTP status codes by <c>BatchIngestOutcomeExtensions.ToActionResult</c>
/// and to CLI exit codes by <c>CliExitCodeMapper.ToExitCode</c>.
/// </summary>
public enum BatchIngestError
{
    EmptyBody,
    InvalidZipArchive,
    MissingRagConfigYaml,
    MissingMetadataRulesYaml,
    MissingQueriesYaml,
    MissingMultilingualGlossaryYaml,
    MetadataRulesMissingDocKindRules,
    QueriesMissingNamedQueries,
    QueriesReferenceUnknownDocKind,
    PathTraversalDetected,
    NoMarkdownFiles,
    QueueFull,
    ChannelWriteFailed,
}
