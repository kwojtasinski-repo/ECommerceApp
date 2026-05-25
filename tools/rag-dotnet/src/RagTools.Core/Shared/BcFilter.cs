namespace RagTools.Core.Shared;

/// <summary>
/// Shared post-search helpers used by Query and ReadDocs pipelines:
/// per-document weight multiplier (from <see cref="RagConfig.GetWeight"/>)
/// and the substring bc/topic filter (matched against breadcrumb and doc title).
/// </summary>
internal static class BcFilter
{
    public static IReadOnlyList<DocumentSearchResult> ApplyWeights(
        IReadOnlyList<DocumentSearchResult> hits,
        RagConfig cfg) =>
        hits.Select(h => h with { Score = h.Score * cfg.GetWeight(h.RelPath) })
            .OrderByDescending(h => h.Score)
            .ToList();

    public static bool Matches(DocumentSearchResult h, string bc)
    {
        var lower = bc.ToLowerInvariant();
        return h.Breadcrumb.ToLowerInvariant().Contains(lower)
            || h.DocTitle.ToLowerInvariant().Contains(lower);
    }
}
