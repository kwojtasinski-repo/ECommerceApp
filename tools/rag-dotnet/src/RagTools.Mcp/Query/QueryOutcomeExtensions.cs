using Microsoft.AspNetCore.Mvc;
using RagTools.Core.Query;

namespace RagTools.Mcp.Query;

/// <summary>
/// HTTP mapping for the typed query outcomes. Every <see cref="QueryError"/> value
/// has exactly one HTTP status code — keep this table in sync with any future
/// <c>CliExitCodeMapper.ToExitCode</c> entries for the query path so the CLI and HTTP
/// surfaces stay aligned. Mirrors <see cref="RagTools.Mcp.Ingest.BatchIngestOutcomeExtensions"/>.
///
/// Success → 200 OK with the <see cref="QueryResponse"/> as the body.
/// Failure → status code per <see cref="StatusFor"/> with the stable JSON envelope:
///   <code>{ "error": "&lt;message&gt;", "code": "&lt;EnumName&gt;", "details": { … } }</code>
/// </summary>
public static class QueryOutcomeExtensions
{
    public static IActionResult ToActionResult(this QueryOutcome outcome) =>
        outcome switch
        {
            QueryOutcome.Success s => new OkObjectResult(s.Response),
            QueryOutcome.Failure f => MapFailure(f.Error, f.Message, f.Details),
            _ => new ObjectResult(new { error = "Unhandled outcome", code = "Unknown" }) { StatusCode = 500 },
        };

    private static ObjectResult MapFailure(
        QueryError error,
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
    public static int StatusFor(QueryError error) => error switch
    {
        QueryError.EmptyQuestion        => 400,
        QueryError.TopKOutOfRange       => 400,
        QueryError.UnknownCollection    => 404,
        QueryError.EmbeddingFailed      => 502,
        QueryError.StoreSearchFailed    => 502,
        QueryError.PostprocessorFailed  => 500,
        _ => 500,
    };
}
