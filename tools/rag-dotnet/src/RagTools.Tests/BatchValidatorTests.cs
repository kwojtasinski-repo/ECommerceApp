using Microsoft.Extensions.Logging.Abstractions;
using RagTools.Core.Ingest;

namespace RagTools.Tests;

/// <summary>
/// Unit tests for <see cref="BatchValidator"/> — pure rules, no ZIP, no I/O.
/// Every reject/warn path lives here. <see cref="ZipBatchParserTests"/> covers only the
/// thin ZIP-orchestration mechanics (temp file, archive open, content readback).
/// </summary>
public sealed class BatchValidatorTests
{
    private const string RagConfigNoGlossary =
        "chunker:\n" +
        "  max_tokens: 512\n" +
        "ranking:\n" +
        "  weights:\n" +
        "    - pattern: \"docs/**\"\n" +
        "      weight: 1.0\n";

    private const string RagConfigWithGlossary =
        "chunker:\n" +
        "  max_tokens: 512\n" +
        "ranking:\n" +
        "  weights:\n" +
        "    - pattern: \"docs/**\"\n" +
        "      weight: 1.0\n" +
        "config_files:\n" +
        "  multilingual_glossary: multilingual-glossary.yaml\n";

    private const string MinimalMetadataYaml = """
        doc_kind_rules:
          - kind: adr_main
            paths: ["docs/adr/**"]
        """;

    private const string MinimalQueriesYaml = """
        named_queries:
          - id: q1
            doc_kind: adr_main
            text: hello
        """;

    private static BatchValidator NewSut() => new(NullLogger<BatchValidator>.Instance);

    private static IReadOnlyList<ZipEntryInfo> Entries(params (string Name, long Length)[] items) =>
        items.Select(i => new ZipEntryInfo(i.Name, i.Length)).ToList();

    private static Func<string, string?> Yaml(
        string? ragConfig = null,
        string? metadataRules = null,
        string? queries = null,
        string? glossary = null) => name => name switch
        {
            "rag-config.yaml"           => ragConfig,
            "metadata-rules.yaml"       => metadataRules,
            "queries.yaml"              => queries,
            "multilingual-glossary.yaml" => glossary,
            _ => null,
        };

    private static ZipParseOutcome.Failure AssertBad(ValidationOutcome outcome) =>
        Assert.IsType<ValidationOutcome.Bad>(outcome).Failure;

    private static ValidationOutcome.Ok AssertOk(ValidationOutcome outcome) =>
        Assert.IsType<ValidationOutcome.Ok>(outcome);

    // ── Required-file presence ────────────────────────────────────────────────

    [Fact]
    public void Missing_RagConfig_ReturnsMissingRagConfigYaml()
    {
        var sut = NewSut();
        var entries = Entries(
            ("metadata-rules.yaml", 100),
            ("queries.yaml", 100),
            ("doc.md", 50));

        var failure = AssertBad(sut.Validate(entries,
            Yaml(metadataRules: MinimalMetadataYaml, queries: MinimalQueriesYaml)));

        Assert.Equal(BatchIngestError.MissingRagConfigYaml, failure.Error);
    }

    [Fact]
    public void Missing_MetadataRules_ReturnsMissingMetadataRulesYaml()
    {
        var sut = NewSut();
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("queries.yaml", 100),
            ("doc.md", 50));

        var failure = AssertBad(sut.Validate(entries,
            Yaml(ragConfig: RagConfigNoGlossary, queries: MinimalQueriesYaml)));

        Assert.Equal(BatchIngestError.MissingMetadataRulesYaml, failure.Error);
    }

    [Fact]
    public void Missing_Queries_ReturnsMissingQueriesYaml()
    {
        var sut = NewSut();
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100),
            ("doc.md", 50));

        var failure = AssertBad(sut.Validate(entries,
            Yaml(ragConfig: RagConfigNoGlossary, metadataRules: MinimalMetadataYaml)));

        Assert.Equal(BatchIngestError.MissingQueriesYaml, failure.Error);
    }

    // ── YAML structure ────────────────────────────────────────────────────────

    [Fact]
    public void MetadataRules_WithoutDocKindRules_Rejected()
    {
        var sut = NewSut();
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100),
            ("queries.yaml", 100),
            ("doc.md", 50));

        var failure = AssertBad(sut.Validate(entries,
            Yaml(ragConfig: RagConfigNoGlossary,
                 metadataRules: "embedder:\n  model: bge\n",
                 queries: MinimalQueriesYaml)));

        Assert.Equal(BatchIngestError.MetadataRulesMissingDocKindRules, failure.Error);
    }

    [Fact]
    public void Queries_WithoutNamedQueries_Rejected()
    {
        var sut = NewSut();
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100),
            ("queries.yaml", 100),
            ("doc.md", 50));

        var failure = AssertBad(sut.Validate(entries,
            Yaml(ragConfig: RagConfigNoGlossary,
                 metadataRules: MinimalMetadataYaml,
                 queries: "embedder: x\n")));

        Assert.Equal(BatchIngestError.QueriesMissingNamedQueries, failure.Error);
    }

    // ── Cross-validation ──────────────────────────────────────────────────────

    [Fact]
    public void Queries_ReferencingUnknownDocKind_Rejected_WithListInDetails()
    {
        var sut = NewSut();
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100),
            ("queries.yaml", 100),
            ("doc.md", 50));
        var queries = """
            named_queries:
              - id: q1
                doc_kind: ghost_kind
                text: hi
              - id: q2
                doc_kind: adr_main
                text: hi
            """;

        var failure = AssertBad(sut.Validate(entries,
            Yaml(ragConfig: RagConfigNoGlossary,
                 metadataRules: MinimalMetadataYaml,
                 queries: queries)));

        Assert.Equal(BatchIngestError.QueriesReferenceUnknownDocKind, failure.Error);
        Assert.NotNull(failure.Details);
        var unknown = Assert.IsAssignableFrom<IEnumerable<string>>(failure.Details!["unknownKinds"]);
        Assert.Contains("ghost_kind", unknown);
        Assert.DoesNotContain("adr_main", unknown);
    }

    [Fact]
    public void CrossValidation_IgnoresCommentLines()
    {
        var sut = NewSut();
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100),
            ("queries.yaml", 100),
            ("doc.md", 50));
        var queries = """
            named_queries:
              - id: q1
                # doc_kind: ghost_kind     <- commented, must be ignored
                doc_kind: adr_main
                text: hi
            """;

        AssertOk(sut.Validate(entries,
            Yaml(ragConfig: RagConfigNoGlossary,
                 metadataRules: MinimalMetadataYaml,
                 queries: queries)));
    }

    // ── Conditional glossary ──────────────────────────────────────────────────

    [Fact]
    public void Glossary_DeclaredButMissing_Rejected()
    {
        var sut = NewSut();
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100),
            ("queries.yaml", 100),
            ("doc.md", 50));

        var failure = AssertBad(sut.Validate(entries,
            Yaml(ragConfig: RagConfigWithGlossary,
                 metadataRules: MinimalMetadataYaml,
                 queries: MinimalQueriesYaml)));

        Assert.Equal(BatchIngestError.MissingMultilingualGlossaryYaml, failure.Error);
    }

    [Fact]
    public void Glossary_DeclaredAndPresent_Ok_NoWarning()
    {
        var sut = NewSut();
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100),
            ("queries.yaml", 100),
            ("multilingual-glossary.yaml", 100),
            ("doc.md", 50));

        var ok = AssertOk(sut.Validate(entries,
            Yaml(ragConfig: RagConfigWithGlossary,
                 metadataRules: MinimalMetadataYaml,
                 queries: MinimalQueriesYaml,
                 glossary: "languages:\n  pl: {}\n")));

        Assert.DoesNotContain(ok.Warnings, w => w.Contains("multilingual", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Glossary_NotDeclared_NotPresent_OkWithWarning()
    {
        var sut = NewSut();
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100),
            ("queries.yaml", 100),
            ("doc.md", 50));

        var ok = AssertOk(sut.Validate(entries,
            Yaml(ragConfig: RagConfigNoGlossary,
                 metadataRules: MinimalMetadataYaml,
                 queries: MinimalQueriesYaml)));

        Assert.Contains(ok.Warnings, w => w.Contains("multilingual_glossary", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MissingRankingWeights_AddsWarning()
    {
        var sut = NewSut();
        var ragConfigNoWeights = """
            chunker:
              max_tokens: 512
            """;
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100),
            ("queries.yaml", 100),
            ("doc.md", 50));

        var ok = AssertOk(sut.Validate(entries,
            Yaml(ragConfig: ragConfigNoWeights,
                 metadataRules: MinimalMetadataYaml,
                 queries: MinimalQueriesYaml)));

        Assert.Contains(ok.Warnings, w => w.Contains("ranking.weights", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MissingChunkerMaxTokens_AddsWarning()
    {
        var sut = NewSut();
        var ragConfigNoMaxTokens = """
            ranking:
              weights:
                - pattern: "docs/**"
                  weight: 1.0
            """;
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100),
            ("queries.yaml", 100),
            ("doc.md", 50));

        var ok = AssertOk(sut.Validate(entries,
            Yaml(ragConfig: ragConfigNoMaxTokens,
                 metadataRules: MinimalMetadataYaml,
                 queries: MinimalQueriesYaml)));

        Assert.Contains(ok.Warnings, w => w.Contains("chunker.max_tokens", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WeightsAndMaxTokensPresent_DoNotAddWarnings()
    {
        var sut = NewSut();
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100),
            ("queries.yaml", 100),
            ("doc.md", 50));

        var ok = AssertOk(sut.Validate(entries,
            Yaml(ragConfig: RagConfigNoGlossary,
                 metadataRules: MinimalMetadataYaml,
                 queries: MinimalQueriesYaml)));

        Assert.DoesNotContain(ok.Warnings, w => w.Contains("ranking.weights", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(ok.Warnings, w => w.Contains("chunker.max_tokens", StringComparison.OrdinalIgnoreCase));
    }

    // ── Per-entry rules ───────────────────────────────────────────────────────

    [Fact]
    public void PathTraversal_Rejected()
    {
        var sut = NewSut();
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100),
            ("queries.yaml", 100),
            ("../escape.md", 50));

        var failure = AssertBad(sut.Validate(entries,
            Yaml(ragConfig: RagConfigNoGlossary,
                 metadataRules: MinimalMetadataYaml,
                 queries: MinimalQueriesYaml)));

        Assert.Equal(BatchIngestError.PathTraversalDetected, failure.Error);
    }

    [Fact]
    public void NonMarkdownFiles_Skipped_WithWarning()
    {
        var sut = NewSut();
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100),
            ("queries.yaml", 100),
            ("doc.md", 50),
            ("image.png", 1024));

        var ok = AssertOk(sut.Validate(entries,
            Yaml(ragConfig: RagConfigNoGlossary,
                 metadataRules: MinimalMetadataYaml,
                 queries: MinimalQueriesYaml)));

        Assert.Single(ok.EligibleDocs);
        Assert.Equal("doc.md", ok.EligibleDocs[0].Name);
        Assert.Contains(ok.Warnings, w => w.Contains("image.png"));
    }

    [Fact]
    public void ZeroByteFiles_Skipped_WithWarning()
    {
        var sut = NewSut();
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100),
            ("queries.yaml", 100),
            ("real.md", 50),
            ("empty.md", 0));

        var ok = AssertOk(sut.Validate(entries,
            Yaml(ragConfig: RagConfigNoGlossary,
                 metadataRules: MinimalMetadataYaml,
                 queries: MinimalQueriesYaml)));

        Assert.Single(ok.EligibleDocs);
        Assert.Equal("real.md", ok.EligibleDocs[0].Name);
        Assert.Contains(ok.Warnings, w => w.Contains("empty.md"));
    }

    [Fact]
    public void NoMarkdownFiles_Rejected()
    {
        var sut = NewSut();
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100),
            ("queries.yaml", 100));

        var failure = AssertBad(sut.Validate(entries,
            Yaml(ragConfig: RagConfigNoGlossary,
                 metadataRules: MinimalMetadataYaml,
                 queries: MinimalQueriesYaml)));

        Assert.Equal(BatchIngestError.NoMarkdownFiles, failure.Error);
    }

    [Fact]
    public void ConfigFiles_NeverAppearInEligibleDocs()
    {
        var sut = NewSut();
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100),
            ("queries.yaml", 100),
            ("doc.md", 50));

        var ok = AssertOk(sut.Validate(entries,
            Yaml(ragConfig: RagConfigNoGlossary,
                 metadataRules: MinimalMetadataYaml,
                 queries: MinimalQueriesYaml)));

        Assert.DoesNotContain(ok.EligibleDocs, e =>
            e.Name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WindowsBackslash_PathTraversal_AlsoRejected()
    {
        var sut = NewSut();
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100),
            ("queries.yaml", 100),
            (@"..\escape.md", 50));

        var failure = AssertBad(sut.Validate(entries,
            Yaml(ragConfig: RagConfigNoGlossary,
                 metadataRules: MinimalMetadataYaml,
                 queries: MinimalQueriesYaml)));

        Assert.Equal(BatchIngestError.PathTraversalDetected, failure.Error);
    }

    [Fact]
    public void Ok_NormalizesWindowsBackslashes_InEligibleDocs()
    {
        var sut = NewSut();
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100),
            ("queries.yaml", 100),
            (@"sub\dir\doc.md", 50));

        var ok = AssertOk(sut.Validate(entries,
            Yaml(ragConfig: RagConfigNoGlossary,
                 metadataRules: MinimalMetadataYaml,
                 queries: MinimalQueriesYaml)));

        Assert.Single(ok.EligibleDocs);
        Assert.Equal("sub/dir/doc.md", ok.EligibleDocs[0].Name);
    }

    // ── Custom companion filenames via config_files ───────────────────────────

    [Fact]
    public void CustomCompanionFilenames_AreHonored_FromConfigFilesMap()
    {
        var sut = NewSut();
        var ragConfigCustom = """
            embedder:
              model: BAAI/bge-m3
            config_files:
              metadata_rules: my-rules.yaml
              queries: my-queries.yaml
            """;
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("my-rules.yaml", 100),
            ("my-queries.yaml", 100),
            ("doc.md", 50));

        Func<string, string?> read = name => name switch
        {
            "rag-config.yaml"  => ragConfigCustom,
            "my-rules.yaml"    => MinimalMetadataYaml,
            "my-queries.yaml"  => MinimalQueriesYaml,
            _ => null,
        };

        var ok = AssertOk(sut.Validate(entries, read));

        // user-declared companion filenames are filtered out of eligible docs
        Assert.Single(ok.EligibleDocs);
        Assert.Equal("doc.md", ok.EligibleDocs[0].Name);
    }

    [Fact]
    public void CustomMetadataRulesFilename_DeclaredButMissing_IsRejected()
    {
        var sut = NewSut();
        var ragConfigCustom = """
            config_files:
              metadata_rules: my-rules.yaml
            """;
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100), // wrong name — declared one is missing
            ("queries.yaml", 100),
            ("doc.md", 50));

        var failure = AssertBad(sut.Validate(entries,
            Yaml(ragConfig: ragConfigCustom,
                 metadataRules: MinimalMetadataYaml,
                 queries: MinimalQueriesYaml)));

        Assert.Equal(BatchIngestError.MissingMetadataRulesYaml, failure.Error);
        Assert.Contains("my-rules.yaml", failure.Message);
    }

    [Fact]
    public void CustomGlossaryFilename_DeclaredAndPresent_NoWarning()
    {
        var sut = NewSut();
        var ragConfigCustom = """
            config_files:
              multilingual_glossary: glossary-pl-de.yaml
            """;
        var entries = Entries(
            ("rag-config.yaml", 100),
            ("metadata-rules.yaml", 100),
            ("queries.yaml", 100),
            ("glossary-pl-de.yaml", 100),
            ("doc.md", 50));

        Func<string, string?> read = name => name switch
        {
            "rag-config.yaml"       => ragConfigCustom,
            "metadata-rules.yaml"   => MinimalMetadataYaml,
            "queries.yaml"          => MinimalQueriesYaml,
            "glossary-pl-de.yaml"   => "languages: { pl: {} }",
            _ => null,
        };

        var ok = AssertOk(sut.Validate(entries, read));

        Assert.DoesNotContain(ok.Warnings, w => w.Contains("multilingual_glossary", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(ok.EligibleDocs, e => e.Name == "glossary-pl-de.yaml");
    }
}
