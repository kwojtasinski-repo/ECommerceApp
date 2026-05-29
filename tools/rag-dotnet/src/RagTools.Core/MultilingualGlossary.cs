using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RagTools.Core;

/// <summary>
/// Query-time expansion glossary that appends English synonym groups for non-English
/// (Polish / German) patterns found in a query.
///
/// Single source of truth: tools/rag/multilingual-glossary.yaml (shared with Python server).
/// Loaded via <see cref="Load"/> — returns an empty, no-op instance when the file is absent
/// so English-only queries degrade gracefully.
/// </summary>
public sealed class MultilingualGlossary
{
    private readonly IReadOnlyList<GlossaryEntry> _entries;

    private MultilingualGlossary(IReadOnlyList<GlossaryEntry> entries) => _entries = entries;

    /// <summary>Singleton empty instance — used when the glossary file is absent.</summary>
    public static readonly MultilingualGlossary Empty = new([]);

    /// <summary>
    /// Load <c>multilingual-glossary.yaml</c> from the resolved <paramref name="glossaryPath"/>.
    /// The path must be absolute and is supplied by the caller (typically <c>RagConfig.GlossaryPath</c>)
    /// — no path construction happens inside this method.
    /// Returns <see cref="Empty"/> when <paramref name="glossaryPath"/> is null, empty, or the
    /// file does not exist, so callers degrade gracefully without special-casing.
    /// </summary>
    public static MultilingualGlossary Load(string? glossaryPath)
    {
        if (string.IsNullOrEmpty(glossaryPath) || !File.Exists(glossaryPath))
            return Empty;

        try
        {
            var yaml = File.ReadAllText(glossaryPath, System.Text.Encoding.UTF8);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            var raw = deserializer.Deserialize<GlossaryFile>(yaml);
            var entries = (raw?.Entries ?? [])
                .Where(e => !string.IsNullOrWhiteSpace(e.English) && e.Patterns?.Count > 0)
                .Select(e => new GlossaryEntry(
                    e.English!,
                    e.Patterns!.Select(p => p.ToLowerInvariant()).ToArray()))
                .ToList();
            return new MultilingualGlossary(entries);
        }
        catch
        {
            return Empty;
        }
    }

    /// <summary>
    /// <summary>
    /// Appends English synonym groups for any non-English pattern found in <paramref name="query"/>.
    /// The expansion is repeated <see cref="ExpansionRepeat"/> times so English terms outweigh
    /// the non-English source tokens in mean pooling (~60% English weight for a 7-word query).
    /// English-only queries are returned unchanged (no ASCII-only patterns in the glossary).
    /// </summary>
    public const int ExpansionRepeat = 3;

    public string Expand(string query)
    {
        if (_entries.Count == 0) return query;

        var lower = query.ToLowerInvariant();
        var additions = new List<string>();

        foreach (var entry in _entries)
        {
            foreach (var pattern in entry.Patterns)
            {
                // Word-boundary-aware match using a non-letter look-around (handles Unicode).
                if (Regex.IsMatch(lower, @"(?<![a-z])" + Regex.Escape(pattern) + @"(?![a-z])"))
                {
                    additions.Add(entry.English);
                    break; // one match per entry is enough
                }
            }
        }

        if (additions.Count == 0) return query;
        var expansion = string.Join(" ", additions);
        return query + string.Concat(Enumerable.Repeat(" " + expansion, ExpansionRepeat));
    }

    /// <summary>
    /// Returns a new <see cref="MultilingualGlossary"/> containing only the entries whose
    /// <c>english</c> key (case-insensitive) is in <paramref name="allowedEnglishKeys"/>.
    /// Used by <c>GlossaryExpansionPreprocessor</c> to honor a per-collection allow-list
    /// stored in <see cref="RagConfigPayload.GlossaryTerms"/>. An empty allow-list returns
    /// <see cref="Empty"/>; callers decide whether to fall back to the full mounted glossary.
    /// </summary>
    public MultilingualGlossary FilterByEnglishKeys(IReadOnlyCollection<string> allowedEnglishKeys)
    {
        if (_entries.Count == 0 || allowedEnglishKeys.Count == 0) return Empty;
        var set = new HashSet<string>(allowedEnglishKeys, StringComparer.OrdinalIgnoreCase);
        var filtered = _entries.Where(e => set.Contains(e.English)).ToList();
        return filtered.Count == 0 ? Empty : new MultilingualGlossary(filtered);
    }

    // ── YAML deserialization models ───────────────────────────────────────────

    private sealed class GlossaryFile
    {
        public List<RawEntry>? Entries { get; init; }
    }

    private sealed class RawEntry
    {
        public string? English { get; init; }
        public List<string>? Patterns { get; init; }
    }

    private sealed record GlossaryEntry(string English, string[] Patterns);
}
