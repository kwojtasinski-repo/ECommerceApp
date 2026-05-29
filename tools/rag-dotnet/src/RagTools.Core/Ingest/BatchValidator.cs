using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RagTools.Core.Ingest;

/// <summary>
/// Single owner of every reject/warn rule for an ingest ZIP. Pure functions over strings
/// and entry descriptors — no streams, no I/O, fully unit-testable.
///
/// YAML structural checks delegate to <see cref="RagConfig.ParseMetadataRules"/>,
/// <see cref="RagConfig.ParseQueries"/>, and <see cref="RagConfig.HasGlossaryConfigDeclaration"/>
/// so parsing stays in one place (shared with file-based ingest via <see cref="RagConfig.Load"/>).
/// </summary>
public sealed class BatchValidator(ILogger<BatchValidator> logger)
{
    private const string RagConfigFilename = "rag-config.yaml";

    /// <summary>
    /// Runs every rule against the supplied entry list. The parser passes entry names and
    /// sizes only; YAML contents are pulled from the ZIP and forwarded via <paramref name="readYamlEntry"/>.
    ///
    /// Companion filenames (metadata-rules / queries / multilingual-glossary) are NOT hardcoded:
    /// they are read from <c>rag-config.yaml.config_files</c> via
    /// <see cref="RagConfig.GetCompanionFilenames"/>, with conventional fallbacks when a
    /// key is absent. Whatever name the user declared is what we look for in the ZIP.
    /// </summary>
    public ValidationOutcome Validate(
        IReadOnlyList<ZipEntryInfo> entries,
        Func<string, string?> readYamlEntry)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(readYamlEntry);

        var names = entries.Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // ── rag-config.yaml is the always-required entry point ───────────────
        if (!names.Contains(RagConfigFilename))
        {
            return Bad(BatchIngestError.MissingRagConfigYaml,
                $"Required file '{RagConfigFilename}' not found in ZIP root.", YamlHint(names));
        }

        var ragConfigYaml = readYamlEntry(RagConfigFilename) ?? string.Empty;

        // Companion filenames declared by the user (or fallback names) ────────
        var companions = RagConfig.GetCompanionFilenames(ragConfigYaml);

        if (!names.Contains(companions.MetadataRules))
        {
            return Bad(BatchIngestError.MissingMetadataRulesYaml,
                $"Required companion file '{companions.MetadataRules}' (declared as metadata_rules in rag-config.yaml) not found in ZIP root.",
                YamlHint(names));
        }

        if (!names.Contains(companions.Queries))
        {
            return Bad(BatchIngestError.MissingQueriesYaml,
                $"Required companion file '{companions.Queries}' (declared as queries in rag-config.yaml) not found in ZIP root.",
                YamlHint(names));
        }

        var metadataRulesYaml = readYamlEntry(companions.MetadataRules) ?? string.Empty;
        var queriesYaml       = readYamlEntry(companions.Queries)       ?? string.Empty;

        // ── metadata-rules structure (typed parse via RagConfig) ──────────────
        MetadataRulesSection? rules;
        try
        {
            rules = RagConfig.ParseMetadataRules(metadataRulesYaml);
        }
        catch (Exception ex)
        {
            return Bad(BatchIngestError.MetadataRulesMissingDocKindRules,
                $"metadata-rules.yaml is not valid YAML: {ex.Message}");
        }
        if (rules?.DocKindRules is null || rules.DocKindRules.Count == 0)
        {
            return Bad(BatchIngestError.MetadataRulesMissingDocKindRules,
                "metadata-rules.yaml must contain at least one doc_kind_rules entry.");
        }

        // ── queries structure (typed parse via RagConfig) ─────────────────────
        List<NamedQueryEntry> queries;
        try
        {
            queries = RagConfig.ParseQueries(queriesYaml);
        }
        catch (Exception ex)
        {
            return Bad(BatchIngestError.QueriesMissingNamedQueries,
                $"queries.yaml is not valid YAML: {ex.Message}");
        }
        if (queries.Count == 0)
        {
            return Bad(BatchIngestError.QueriesMissingNamedQueries,
                "queries.yaml must contain at least one named_queries entry.");
        }

        // ── cross-validation: every doc_kind in queries.yaml must be declared in metadata-rules.yaml ──
        var knownKinds = rules.DocKindRules
            .Select(r => r.Kind)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var badKinds = queries
            .Select(q => q.DocKind)
            .Where(k => !string.IsNullOrWhiteSpace(k) && !knownKinds.Contains(k!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();
        if (badKinds.Count > 0)
        {
            return Bad(BatchIngestError.QueriesReferenceUnknownDocKind,
                $"queries.yaml references unknown doc_kind(s): [{string.Join(", ", badKinds!)}]. " +
                "Add matching rules to metadata-rules.yaml.",
                new Dictionary<string, object?> { ["unknownKinds"] = badKinds });
        }

        // ── conditional glossary requirement ──────────────────────────────────
        var warnings        = new List<string>();
        var glossaryPresent = names.Contains(companions.Glossary);
        if (companions.GlossaryDeclared && !glossaryPresent)
        {
            return Bad(BatchIngestError.MissingMultilingualGlossaryYaml,
                $"rag-config.yaml declares multilingual_glossary='{companions.Glossary}' but that file is missing from the ZIP.",
                YamlHint(names));
        }
        if (!companions.GlossaryDeclared && !glossaryPresent)
        {
            warnings.Add("rag-config.yaml does not declare multilingual_glossary — Polish/German query expansion will be reduced.");
        }

        if (!HasRankingWeights(ragConfigYaml))
        {
            warnings.Add("rag-config.yaml has no ranking.weights — using server defaults; query relevance may be suboptimal.");
        }
        if (!HasChunkerMaxTokens(ragConfigYaml))
        {
            warnings.Add("rag-config.yaml has no chunker.max_tokens — using 512 (embedder native length).");
        }

        // Filenames that must NEVER appear in the eligible-docs list, regardless
        // of extension. Includes rag-config.yaml plus the user-declared companion
        // filenames (which may have non-default names like "my-rules.yaml").
        var configFilenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            RagConfigFilename,
            companions.MetadataRules,
            companions.Queries,
            companions.Glossary,
        };

        // ── per-entry filtering: path traversal, extension, zero-byte ─────────
        var eligible = new List<ZipEntryInfo>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.Name.EndsWith('/')) continue;
            if (configFilenames.Contains(entry.Name)) continue;

            var normalized = entry.Name.Replace('\\', '/');
            var isAbsolute =
                normalized.StartsWith('/') ||
                (normalized.Length >= 2 && normalized[1] == ':');
            if (normalized.Split('/').Any(p => p == "..") || isAbsolute)
            {
                return Bad(BatchIngestError.PathTraversalDetected,
                    $"Path traversal detected in ZIP entry '{entry.Name}'.",
                    new Dictionary<string, object?> { ["entry"] = entry.Name });
            }

            if (!entry.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"Skipped non-.md file: '{entry.Name}'");
                continue;
            }

            if (entry.Length == 0)
            {
                warnings.Add($"Skipped zero-byte file: '{entry.Name}'");
                continue;
            }

            eligible.Add(entry with { Name = normalized });
        }

        if (eligible.Count == 0)
        {
            return Bad(BatchIngestError.NoMarkdownFiles,
                "ZIP contains no .md document files.");
        }

        logger.LogDebug(
            "Batch validation OK — {DocCount} eligible docs, {WarnCount} warnings, glossary declared: {GlossaryDeclared}",
            eligible.Count, warnings.Count, companions.GlossaryDeclared);

        return new ValidationOutcome.Ok(rules, eligible, warnings);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static ValidationOutcome.Bad Bad(
        BatchIngestError error, string message,
        IReadOnlyDictionary<string, object?>? details = null) =>
        new(new ZipParseOutcome.Failure(error, message, details));

    private static Dictionary<string, object?> YamlHint(HashSet<string> names)
    {
        var yaml = names
            .Where(n => n.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                     || n.EndsWith(".yml",  StringComparison.OrdinalIgnoreCase))
            .ToList();
        return new Dictionary<string, object?> { ["yamlFilesPresent"] = yaml };
    }

    private static bool HasRankingWeights(string ragConfigYaml)
    {
        var raw = DeserializeYaml(ragConfigYaml);
        if (!raw.TryGetValue("ranking", out var rankingObj) || rankingObj is not Dictionary<object, object> ranking)
            return false;
        return ranking.TryGetValue("weights", out var weightsObj)
            && weightsObj is IEnumerable<object> weights
            && weights.Any();
    }

    private static bool HasChunkerMaxTokens(string ragConfigYaml)
    {
        var raw = DeserializeYaml(ragConfigYaml);
        if (!raw.TryGetValue("chunker", out var chunkerObj) || chunkerObj is not Dictionary<object, object> chunker)
            return false;
        return chunker.ContainsKey("max_tokens") && chunker["max_tokens"] is not null;
    }

    private static Dictionary<object, object> DeserializeYaml(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml)) return [];
        try
        {
            return new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build()
                .Deserialize<Dictionary<object, object>>(yaml) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
