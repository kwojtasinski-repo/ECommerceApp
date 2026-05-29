using System.Text.RegularExpressions;

namespace RagTools.Core;

/// <summary>
/// Resolves the ranking weight for a document path based on the
/// <c>ranking.weights</c> glob list in rag-config.yaml.
///
/// Single source of truth — previously duplicated:
///   • <c>FileIngestor.ResolveWeight</c> (CLI) — applied per-file weight
///   • <c>IngestWorker.ProcessJobAsync</c> (HTTP) — hardcoded weight = 1.0 (BUG)
/// </summary>
public static class RankingWeightResolver
{
    /// <summary>
    /// Resolves the per-file weight. Stub-file rule applies first
    /// (files under <c>/example-implementation/</c> below
    /// <see cref="RankingSection.StubByteThreshold"/> bytes get 0.05).
    /// Then the first matching glob in <see cref="RankingSection.Weights"/> wins.
    /// Returns 1.0 if no rule matches.
    /// </summary>
    public static float Resolve(string relPath, int fileSizeBytes, RankingSection ranking)
        => Resolve(relPath, fileSizeBytes, ranking.Weights, ranking.StubByteThreshold);

    /// <summary>
    /// Overload using an explicit weight list + stub-byte threshold. Used by the HTTP path,
    /// which resolves <paramref name="weights"/> from the per-collection
    /// <see cref="RagConfigPayload.Weights"/> (ADR-0028 Phase 3 / P3-3c) while the stub-byte
    /// threshold stays mounted-only.
    /// </summary>
    public static float Resolve(string relPath, int fileSizeBytes, List<WeightEntry> weights, int stubByteThreshold)
    {
        var p = relPath.Replace('\\', '/');
        if (fileSizeBytes < stubByteThreshold && p.Contains("/example-implementation/"))
        {
            return 0.05f;
        }

        foreach (var entry in weights)
        {
            if (GlobMatch(p, entry.Pattern))
            {
                return entry.Weight;
            }
        }

        return 1.0f;
    }

    /// <summary>
    /// Minimal glob → regex matcher. Supports <c>**</c> (any path),
    /// <c>*</c> (single segment), and <c>?</c> (single non-slash char).
    /// </summary>
    public static bool GlobMatch(string path, string glob)
    {
        var pattern = "^" +
            Regex.Escape(glob)
                 .Replace(@"\*\*", "§§")
                 .Replace(@"\*", "[^/]*")
                 .Replace(@"\?", "[^/]")
                 .Replace("§§", ".*")
            + "$";
        return Regex.IsMatch(path, pattern);
    }
}
