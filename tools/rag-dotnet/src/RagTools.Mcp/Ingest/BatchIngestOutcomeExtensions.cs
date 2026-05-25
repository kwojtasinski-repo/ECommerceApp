using Microsoft.AspNetCore.Mvc;
using RagTools.Core.Ingest;

namespace RagTools.Mcp.Ingest;

/// <summary>
/// HTTP mapping for the typed ingest outcomes. Every <see cref="BatchIngestError"/> value
/// has exactly one HTTP status code — keep this table in sync with
/// <c>CliExitCodeMapper.ToExitCode</c> when one is added so the CLI and HTTP surfaces
/// stay aligned.
///
/// Success → 202 Accepted with the <see cref="BatchIngestResponse"/> as the body.
/// Failure → status code per <see cref="StatusFor"/> with a stable JSON envelope:
///   <code>{ "error": "&lt;message&gt;", "code": "&lt;EnumName&gt;", "details": { … } }</code>
/// </summary>
public static class BatchIngestOutcomeExtensions
{
    public static IActionResult ToActionResult(this BatchIngestOutcome outcome) =>
        outcome switch
        {
            BatchIngestOutcome.Success s => new AcceptedResult(location: (string?)null, value: s.Response),
            BatchIngestOutcome.Failure f => MapFailure(f.Error, f.Message, f.Details),
            _ => new ObjectResult(new { error = "Unhandled outcome", code = "Unknown" }) { StatusCode = 500 },
        };

    /// <summary>
    /// Maps a parser-stage failure (raised before the request reaches
    /// <see cref="IBatchIngestService"/>) using the same status table.
    /// </summary>
    public static IActionResult ToActionResult(this ZipParseOutcome.Failure failure) =>
        MapFailure(failure.Error, failure.Message, failure.Details);

    private static ObjectResult MapFailure(
        BatchIngestError error,
        string message,
        IReadOnlyDictionary<string, object?>? details)
    {
        var body = new Dictionary<string, object?>
        {
            ["error"] = message,
            ["code"]  = error.ToString(),
        };
        if (details is not null && details.Count > 0)
            body["details"] = details;
        return new ObjectResult(body) { StatusCode = StatusFor(error) };
    }

    /// <summary>Public so unit tests can pin the mapping table.</summary>
    public static int StatusFor(BatchIngestError error) => error switch
    {
        BatchIngestError.EmptyBody                        => 400,
        BatchIngestError.InvalidZipArchive                => 400,
        BatchIngestError.MissingRagConfigYaml             => 400,
        BatchIngestError.MissingMetadataRulesYaml         => 400,
        BatchIngestError.MissingQueriesYaml               => 400,
        BatchIngestError.MissingMultilingualGlossaryYaml  => 400,
        BatchIngestError.MetadataRulesMissingDocKindRules => 400,
        BatchIngestError.QueriesMissingNamedQueries       => 400,
        BatchIngestError.QueriesReferenceUnknownDocKind   => 400,
        BatchIngestError.PathTraversalDetected            => 400,
        BatchIngestError.NoMarkdownFiles                  => 400,
        BatchIngestError.QueueFull                        => 503,
        BatchIngestError.ChannelWriteFailed               => 500,
        _ => 500,
    };
}
