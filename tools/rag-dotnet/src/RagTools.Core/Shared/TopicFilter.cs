namespace RagTools.Core.Shared;

/// <summary>
/// Shared post-search helpers used by Query and ReadDocs pipelines:
/// per-document weight multiplier and the substring topic / bounded-context
/// filter (matched against breadcrumb and doc title).
///
/// Weights are passed explicitly so callers can source them from either the
/// mounted <see cref="RagConfig.Ranking"/> or a per-collection <see cref="RagConfigPayload.Weights"/>.
/// </summary>
internal static class TopicFilter
{
    public static IReadOnlyList<DocumentSearchResult> ApplyWeights(
        IReadOnlyList<DocumentSearchResult> hits,
        IReadOnlyList<WeightEntry> weights) =>
        hits.Select(h => h with { Score = h.Score * RagConfig.GetWeight(weights, h.RelPath) })
            .OrderByDescending(h => h.Score)
            .ToList();

    public static bool Matches(DocumentSearchResult h, string topic)
    {
        var lower = topic.ToLowerInvariant();
        return h.Breadcrumb.ToLowerInvariant().Contains(lower)
            || h.DocTitle.ToLowerInvariant().Contains(lower);
    }
}
