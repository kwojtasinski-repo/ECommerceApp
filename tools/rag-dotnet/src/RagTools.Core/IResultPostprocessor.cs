namespace RagTools.Core;

/// <summary>
/// Post-retrieval processing hook.  Runs AFTER Qdrant search, ApplyWeights, and the bc
/// substring filter — receives the final candidate list and may re-rank, trim, or augment.
///
/// Implementations are ordered and called sequentially in <c>RagTools.QueryDocs()</c>.
/// No built-in implementation ships with this change — this is a pure extension point.
///
/// Registration example:
/// <code>
///   services.AddSingleton&lt;IResultPostprocessor, MyScoreBooster&gt;();
/// </code>
/// </summary>
public interface IResultPostprocessor
{
    Task<IReadOnlyList<DocumentSearchResult>> ProcessAsync(
        IReadOnlyList<DocumentSearchResult> hits,
        QueryContext ctx,
        CancellationToken ct = default);
}
