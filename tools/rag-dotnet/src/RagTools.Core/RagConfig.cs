using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RagTools.Core;

/// <summary>
/// Strongly-typed view over config.yaml. Mirrors the Python Config dataclass.
/// Load with RagConfig.Load(path).
/// </summary>
public sealed class RagConfig
{
    // Raw deserialized YAML sections.
    public SourceSection Source { get; init; } = new();
    public EmbedderSection Embedder { get; init; } = new();
    public VectorStoreSection VectorStore { get; init; } = new();
    public ChunkerSection Chunker { get; init; } = new();
    public RankingSection Ranking { get; init; } = new();
    public QuerySection Query { get; init; } = new();
    public StorageSection Storage { get; init; } = new();
    public MetadataRulesSection MetadataRules { get; init; } = new();
    public List<NamedQueryEntry> NamedQueries { get; init; } = [];

    /// <summary>
    /// Absolute path of the config.yaml this instance was loaded from. Set by <see cref="Load"/>.
    /// Drives <see cref="Workspace"/> derivation (Python parity: config_path.parents[2]).
    /// </summary>
    public string? LoadedFrom { get; init; }

    /// <summary>
    /// Absolute path to <c>multilingual-glossary.yaml</c>, resolved by <see cref="Load"/> via the
    /// companion-file rules (config_files key → fallback filename, same folder as config.yaml).
    /// Null when the file was not found — no path construction happens outside <see cref="Load"/>.
    /// Supports <c>RAG_GLOSSARY</c> env var override (Docker / per-file mount parity with Python).
    /// </summary>
    public string? GlossaryPath { get; init; }

    // ── Computed properties ───────────────────────────────────────────────

    /// <summary>Qdrant collection name. RAG_COLLECTION env var wins over config.</summary>
    public string Collection =>
        Environment.GetEnvironmentVariable("RAG_COLLECTION")
        ?? VectorStore.Collection
        ?? "rag_docs";

    /// <summary>
    /// Absolute repo root. Priority (Python parity):
    ///   1. config.yaml directory's grandparent (when config sits at &lt;repo&gt;/tools/rag/config.yaml)
    ///   2. RAG_WORKSPACE env var
    ///   3. <see cref="Directory.GetCurrentDirectory"/>
    /// Prevents shell env from leaking into explicit --config runs.
    /// </summary>
    public string Workspace => DeriveWorkspace(LoadedFrom);

    internal static string DeriveWorkspace(string? configPath)
    {
        if (!string.IsNullOrEmpty(configPath))
        {
            try
            {
                var full = Path.GetFullPath(configPath);
                var dir = Path.GetDirectoryName(full);
                var parent = dir is null ? null : Path.GetDirectoryName(dir);
                var grand = parent is null ? null : Path.GetDirectoryName(parent);
                if (grand is not null && Directory.Exists(grand))
                    return grand;
            }
            catch (ArgumentException) { /* fall through */ }
        }
        return Environment.GetEnvironmentVariable("RAG_WORKSPACE")
            ?? Directory.GetCurrentDirectory();
    }

    /// <summary>Static escape hatch for callers without a RagConfig instance.</summary>
    public static string RepoRoot =>
        Environment.GetEnvironmentVariable("RAG_WORKSPACE")
        ?? Directory.GetCurrentDirectory();

    public string ManifestAbsPath =>
        Path.Combine(Workspace, Storage.ManifestPath ?? ".rag/manifest.json");

    public string QdrantUrl =>
        VectorStore.Url ?? "http://localhost:6333";

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Load config.yaml and merge companion metadata-rules.yaml and queries.yaml.
    ///
    /// File resolution for companion files (no hardcoded paths):
    ///   1. config.yaml[config_files][&lt;key&gt;] — relative path from config.yaml's directory
    ///   2. &lt;config.yaml directory&gt;/&lt;fallback filename&gt; — convention, same folder
    ///   Returns without merging if the companion file does not exist.
    /// </summary>
    public static RagConfig Load(string? configPath = null)
    {
        configPath = ResolveConfigPath(configPath);

        var configDir = Path.GetDirectoryName(configPath)!;
        var yaml = File.ReadAllText(configPath);
        var deserializer = BuildDeserializer();
        var cfg = deserializer.Deserialize<RagConfig>(yaml);

        // Read config_files section from raw YAML to resolve companion paths.
        var rawDict = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<Dictionary<object, object>>(File.ReadAllText(configPath));

        var configFiles = GetNestedDict(rawDict, "config_files");

        // Merge metadata-rules.yaml into MetadataRules.
        var rulesPath = ResolveCompanionPath(configDir, configFiles, "metadata_rules", "metadata-rules.yaml");
        MetadataRulesSection? mergedRules = null;
        if (rulesPath is not null && File.Exists(rulesPath))
        {
            var rulesYaml = File.ReadAllText(rulesPath);
            mergedRules = deserializer.Deserialize<MetadataRulesSection>(rulesYaml);
        }

        // Merge queries.yaml into NamedQueries.
        var queriesPath = ResolveCompanionPath(configDir, configFiles, "queries", "queries.yaml");
        List<NamedQueryEntry>? mergedQueries = null;
        if (queriesPath is not null && File.Exists(queriesPath))
        {
            var queriesYaml = File.ReadAllText(queriesPath);
            var queriesWrapper = deserializer.Deserialize<QueriesWrapper>(queriesYaml);
            mergedQueries = queriesWrapper?.NamedQueries;
        }

        // Resolve multilingual-glossary.yaml path (not loaded here — left to MultilingualGlossary.Load).
        // RAG_GLOSSARY env var overrides config_files declaration (Docker / per-file mount support).
        var glossaryPath = Environment.GetEnvironmentVariable("RAG_GLOSSARY")
            is string envGlossary && File.Exists(envGlossary)
                ? envGlossary
                : ResolveCompanionPath(configDir, configFiles, "multilingual_glossary", "multilingual-glossary.yaml");
        var resolvedGlossaryPath = glossaryPath is not null && File.Exists(glossaryPath) ? glossaryPath : null;

        return new RagConfig
        {
            LoadedFrom = Path.GetFullPath(configPath),
            Source = cfg.Source,
            Embedder = cfg.Embedder,
            VectorStore = cfg.VectorStore,
            Chunker = cfg.Chunker,
            Ranking = cfg.Ranking,
            Query = cfg.Query,
            Storage = cfg.Storage,
            MetadataRules = mergedRules ?? cfg.MetadataRules,
            NamedQueries = mergedQueries ?? cfg.NamedQueries,
            GlossaryPath = resolvedGlossaryPath,
        };
    }

    /// <summary>
    /// 3-way config discovery (Python parity):
    ///   1. explicit <paramref name="configPath"/> argument (e.g. from CLI)
    ///   2. RAG_CONFIG env var
    ///   3. RAG_WORKSPACE env var → &lt;ws&gt;/tools/rag/config.yaml
    ///   4. AppContext.BaseDirectory/config.yaml fallback (Docker / published bundle)
    /// </summary>
    public static string ResolveConfigPath(string? configPath)
    {
        if (!string.IsNullOrEmpty(configPath))
            return Path.GetFullPath(configPath);

        var envCfg = Environment.GetEnvironmentVariable("RAG_CONFIG");
        if (!string.IsNullOrEmpty(envCfg))
            return Path.GetFullPath(envCfg);

        var envWs = Environment.GetEnvironmentVariable("RAG_WORKSPACE");
        if (!string.IsNullOrEmpty(envWs))
        {
            var derived = Path.Combine(envWs, "tools", "rag", "config.yaml");
            if (File.Exists(derived))
                return Path.GetFullPath(derived);
        }

        return Path.Combine(AppContext.BaseDirectory, "config.yaml");
    }

    private static string? ResolveCompanionPath(
        string configDir,
        Dictionary<string, string>? configFiles,
        string key,
        string fallbackName)
    {
        if (configFiles is not null && configFiles.TryGetValue(key, out var declared))
            return Path.Combine(configDir, declared);
        return Path.Combine(configDir, fallbackName);
    }

    private static Dictionary<string, string>? GetNestedDict(
        Dictionary<object, object>? raw, string key)
    {
        if (raw is null || !raw.TryGetValue(key, out var value)) return null;
        if (value is not Dictionary<object, object> nested) return null;
        return nested.ToDictionary(
            kv => kv.Key.ToString()!,
            kv => kv.Value?.ToString() ?? string.Empty);
    }

    private static IDeserializer BuildDeserializer() =>
        new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Extract ADR ID from a repo-relative path using config patterns. Returns null if none match.</summary>
    public string? DetectAdrId(string relPath)
    {
        var p = relPath.Replace('\\', '/');
        var patterns = MetadataRules.AdrIdPatterns ?? [];
        foreach (var entry in patterns)
        {
            if (string.IsNullOrWhiteSpace(entry.Pattern)) continue;
            // Convert Python-style named groups (?P<name>...) to .NET-style (?<name>...)
            var netPattern = entry.Pattern.Replace("(?P<", "(?<");
            var m = Regex.Match(p, netPattern);
            if (m.Success && m.Groups["id"].Success)
                return m.Groups["id"].Value;
        }
        return null;
    }

    /// <summary>Classify a document by its repo-relative path using config glob rules (first match wins).</summary>
    public string DetectDocKind(string relPath)
    {
        var p = relPath.Replace('\\', '/');
        var rules = MetadataRules.DocKindRules ?? [];
        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Glob) || string.IsNullOrWhiteSpace(rule.Kind))
                continue;
            if (GlobMatch(p, rule.Glob))
                return rule.Kind;
        }
        return "other";
    }

    /// <summary>
    /// Returns the configured weight multiplier for a document path.
    /// Iterates <see cref="RankingSection.Weights"/> — first matching glob wins.
    /// Returns 1.0 if no rule matches (neutral — score unchanged).
    /// </summary>
    public float GetWeight(string relPath)
    {
        var p = relPath.Replace('\\', '/');
        foreach (var entry in Ranking.Weights)
        {
            if (!string.IsNullOrWhiteSpace(entry.Pattern) && GlobMatch(p, entry.Pattern))
                return entry.Weight;
        }
        return 1.0f;
    }

    // Minimal glob matcher: supports ** (any path segment), * (any chars within segment), ? (any single char).
    private static bool GlobMatch(string path, string glob)
    {
        // Delegate to FileSystemGlobbing via a simple regex translation.
        // Convert glob to regex: ** → .*, * → [^/]*, ? → [^/]
        var pattern = "^" +
            Regex.Escape(glob)
                 .Replace(@"\*\*", "§§")   // placeholder for **
                 .Replace(@"\*", "[^/]*")
                 .Replace(@"\?", "[^/]")
                 .Replace("§§", ".*")
            + "$";
        return Regex.IsMatch(path, pattern);
    }
}

// ── Section types (mapped by YamlDotNet) ──────────────────────────────────────

public sealed class SourceSection
{
    public List<string> Roots { get; init; } = [];
    public List<string> ExcludeGlobs { get; init; } = [];
}

public sealed class EmbedderSection
{
    public string Model { get; init; } = "paraphrase-multilingual-MiniLM-L12-v2";
    public string Device { get; init; } = "cpu";
    public int BatchSize { get; init; } = 32;
}

public sealed class VectorStoreSection
{
    public string? Collection { get; init; }
    public string? Url { get; init; }
}

public sealed class ChunkerSection
{
    public int MaxTokens { get; init; } = 400;
    public int OverlapTokens { get; init; } = 50;
    public int MinTokens { get; init; } = 30;
}

public sealed class RankingSection
{
    public int StubByteThreshold { get; init; } = 400;
    public List<WeightEntry> Weights { get; init; } = [];
}

public sealed class WeightEntry
{
    public string Pattern { get; init; } = "*";
    public float Weight { get; init; } = 1.0f;
}

public sealed class QuerySection
{
    public int TopK { get; init; } = 5;
    public float ScoreThreshold { get; init; } = 0.3f;
}

public sealed class StorageSection
{
    public string ManifestPath { get; init; } = ".rag/manifest.json";
    public string? StatsPath { get; init; }
}

public sealed class MetadataRulesSection
{
    public List<AdrIdPatternEntry>? AdrIdPatterns { get; init; }
    public List<DocKindRuleEntry>? DocKindRules { get; init; }
}

public sealed class AdrIdPatternEntry
{
    public string Pattern { get; init; } = string.Empty;
}

public sealed class DocKindRuleEntry
{
    public string Glob { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
}

public sealed class QueriesWrapper
{
    public List<NamedQueryEntry> NamedQueries { get; init; } = [];
}

public sealed class NamedQueryEntry
{
    public string Name { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string? DocKind { get; init; }
    public string? AdrId { get; init; }
    public int? TopK { get; init; }
}
