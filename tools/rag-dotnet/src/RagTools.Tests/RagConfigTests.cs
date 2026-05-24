using RagTools.Core;
using Xunit;

namespace RagTools.Tests;

/// <summary>
/// Tests for RagConfig YAML deserialization and computed properties.
/// Uses temp directories with minimal in-memory config strings — no real files from the repo.
/// </summary>
public class RagConfigTests : IDisposable
{
    private readonly string _tempDir;

    public RagConfigTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ragconfig-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string WriteConfig(string yaml)
    {
        var path = Path.Combine(_tempDir, "rag-config.yaml");
        File.WriteAllText(path, yaml);
        return path;
    }

    private static string MinimalConfig(string extras = "") => $"""
        source:
          roots:
            - docs
          exclude_globs: []
        embedder:
          model: "test-model"
          dimensions: 384
          device: "cpu"
          batch_size: 32
        chunker:
          max_tokens: 800
          min_tokens: 40
          overlap_tokens: 80
          split_on_headings: [1, 2, 3]
        vector_store:
          backend: qdrant
          mode: memory
          collection: test_collection
          url: "http://localhost:6333"
        ranking:
          weights: []
        query:
          default_top_k: 5
          fetch_k: 20
          score_threshold: 0.3
        storage:
          manifest_path: ".rag/manifest.json"
        {extras}
        """;

    // ── Load / deserialization ────────────────────────────────────────────────

    [Fact]
    public void Load_MinimalConfig_DoesNotThrow()
    {
        var path = WriteConfig(MinimalConfig());
        var cfg = RagConfig.Load(path);
        Assert.NotNull(cfg);
    }

    [Fact]
    public void Load_SetsSourceRoots()
    {
        var path = WriteConfig(MinimalConfig());
        var cfg = RagConfig.Load(path);
        Assert.Contains("docs", cfg.Source.Roots);
    }

    [Fact]
    public void Load_SetsEmbedderModel()
    {
        var path = WriteConfig(MinimalConfig());
        var cfg = RagConfig.Load(path);
        Assert.Equal("test-model", cfg.Embedder.Model);
    }

    [Fact]
    public void Load_SetsCollection()
    {
        var path = WriteConfig(MinimalConfig());
        var cfg = RagConfig.Load(path);
        Assert.Equal("test_collection", cfg.VectorStore.Collection);
    }

    [Fact]
    public void Load_SetsChunkerMaxTokens()
    {
        var path = WriteConfig(MinimalConfig());
        var cfg = RagConfig.Load(path);
        Assert.Equal(800, cfg.Chunker.MaxTokens);
    }

    [Fact]
    public void Load_SetsChunkerMinTokens()
    {
        var path = WriteConfig(MinimalConfig());
        var cfg = RagConfig.Load(path);
        Assert.Equal(40, cfg.Chunker.MinTokens);
    }

    [Fact]
    public void Load_SetsQueryScoreThreshold()
    {
        var path = WriteConfig(MinimalConfig());
        var cfg = RagConfig.Load(path);
        Assert.Equal(0.3f, cfg.Query.ScoreThreshold, precision: 4);
    }

    [Fact]
    public void Load_SetsQueryDefaultTopK()
    {
        var path = WriteConfig(MinimalConfig());
        var cfg = RagConfig.Load(path);
        Assert.Equal(5, cfg.Query.DefaultTopK);
    }

    [Fact]
    public void Load_SetsQueryFetchK()
    {
        var path = WriteConfig(MinimalConfig());
        var cfg = RagConfig.Load(path);
        Assert.Equal(20, cfg.Query.FetchK);
    }

    [Fact]
    public void Load_SetsChunkerSplitOnHeadings()
    {
        var path = WriteConfig(MinimalConfig());
        var cfg = RagConfig.Load(path);
        Assert.Equal([1, 2, 3], cfg.Chunker.SplitLevels);
    }

    [Fact]
    public void Load_SetsManifestPath()
    {
        var path = WriteConfig(MinimalConfig());
        var cfg = RagConfig.Load(path);
        Assert.Equal(".rag/manifest.json", cfg.Storage.ManifestPath);
    }

    // ── Computed properties ───────────────────────────────────────────────────

    [Fact]
    public void Collection_ReturnsConfigValue_WhenEnvVarNotSet()
    {
        Environment.SetEnvironmentVariable("RAG_COLLECTION", null);
        var path = WriteConfig(MinimalConfig());
        var cfg = RagConfig.Load(path);
        Assert.Equal("test_collection", cfg.Collection);
    }

    [Fact]
    public void Collection_ReturnsEnvVar_WhenSet()
    {
        Environment.SetEnvironmentVariable("RAG_COLLECTION", "override_collection");
        try
        {
            var path = WriteConfig(MinimalConfig());
            var cfg = RagConfig.Load(path);
            Assert.Equal("override_collection", cfg.Collection);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RAG_COLLECTION", null);
        }
    }

    [Fact]
    public void QdrantUrl_ReturnsConfigUrl()
    {
        var path = WriteConfig(MinimalConfig());
        var cfg = RagConfig.Load(path);
        Assert.Equal("http://localhost:6333", cfg.QdrantUrl);
    }

    [Fact]
    public void ManifestAbsPath_IsAbsolute()
    {
        var path = WriteConfig(MinimalConfig());
        var cfg = RagConfig.Load(path);
        Assert.True(Path.IsPathRooted(cfg.ManifestAbsPath));
    }

    // ── Companion file merging — metadata-rules.yaml ──────────────────────────

    [Fact]
    public void Load_MergesMetadataRules_WhenFilePresent()
    {
        WriteConfig(MinimalConfig());
        var rulesPath = Path.Combine(_tempDir, "metadata-rules.yaml");
        File.WriteAllText(rulesPath, """
            adr_id_patterns:
              - pattern: "docs/adr/(?P<id>\\d{4})/"
            doc_kind_rules:
              - glob: "docs/adr/**"
                kind: "adr_main"
            """);

        var cfg = RagConfig.Load(Path.Combine(_tempDir, "rag-config.yaml"));

        Assert.NotNull(cfg.MetadataRules.AdrIdPatterns);
        Assert.Single(cfg.MetadataRules.AdrIdPatterns!);
        Assert.NotNull(cfg.MetadataRules.DocKindRules);
        Assert.Single(cfg.MetadataRules.DocKindRules!);
    }

    [Fact]
    public void Load_EmptyMetadataRules_WhenFileAbsent()
    {
        var path = WriteConfig(MinimalConfig());
        var cfg = RagConfig.Load(path);
        // No metadata-rules.yaml in temp dir → should use defaults (empty).
        Assert.Null(cfg.MetadataRules.AdrIdPatterns);
    }

    // ── Companion file merging — queries.yaml ────────────────────────────────

    [Fact]
    public void Load_MergesNamedQueries_WhenFilePresent()
    {
        WriteConfig(MinimalConfig());
        var queriesPath = Path.Combine(_tempDir, "queries.yaml");
        File.WriteAllText(queriesPath, """
            named_queries:
              - name: "test_query"
                question: "What is DDD?"
                top_k: 3
            """);

        var cfg = RagConfig.Load(Path.Combine(_tempDir, "rag-config.yaml"));

        Assert.Single(cfg.NamedQueries);
        Assert.Equal("test_query", cfg.NamedQueries[0].Name);
        Assert.Equal("What is DDD?", cfg.NamedQueries[0].Question);
        Assert.Equal(3, cfg.NamedQueries[0].TopK);
    }

    [Fact]
    public void Load_EmptyNamedQueries_WhenFileAbsent()
    {
        var path = WriteConfig(MinimalConfig());
        var cfg = RagConfig.Load(path);
        Assert.Empty(cfg.NamedQueries);
    }

    // ── Ranking weights ───────────────────────────────────────────────────────

    [Fact]
    public void Load_SetsRankingWeights()
    {
        var yaml = MinimalConfig() + """

            """;
        var path = WriteConfig("""
            source:
              roots: [docs]
              exclude_globs: []
            embedder:
              model: "m"
              dimensions: 384
              device: cpu
              batch_size: 32
            chunker:
              max_tokens: 800
              min_tokens: 40
              overlap_tokens: 80
              split_on_headings: [1, 2, 3]
            vector_store:
              backend: qdrant
              mode: memory
              collection: col
              url: "http://localhost:6333"
            ranking:
              weights:
                - pattern: "docs/adr/**"
                  weight: 1.2
                - pattern: "docs/**"
                  weight: 0.9
            query:
              default_top_k: 5
              fetch_k: 20
              score_threshold: 0.3
            storage:
              manifest_path: ".rag/manifest.json"
            """);

        var cfg = RagConfig.Load(path);

        Assert.Equal(2, cfg.Ranking.Weights.Count);
        Assert.Equal("docs/adr/**", cfg.Ranking.Weights[0].Pattern);
        Assert.Equal(1.2f, cfg.Ranking.Weights[0].Weight, precision: 4);
    }

    // ── DetectAdrId ──────────────────────────────────────────────────────────

    [Fact]
    public void DetectAdrId_WithPattern_MatchesAdrPath()
    {
        WriteConfig(MinimalConfig());
        var rulesPath = Path.Combine(_tempDir, "metadata-rules.yaml");
        File.WriteAllText(rulesPath, """
            adr_id_patterns:
              - pattern: "docs/adr/(?<id>\\d{4})/"
            doc_kind_rules: []
            """);

        var cfg = RagConfig.Load(Path.Combine(_tempDir, "rag-config.yaml"));
        var id = cfg.DetectAdrId("docs/adr/0016/0016-coupons.md");

        Assert.Equal("0016", id);
    }

    [Fact]
    public void DetectAdrId_NoPatterns_ReturnsNull()
    {
        var path = WriteConfig(MinimalConfig());
        var cfg = RagConfig.Load(path);
        var id = cfg.DetectAdrId("docs/adr/0016/0016-coupons.md");
        Assert.Null(id);
    }

    // ─── Workspace / config discovery ──────────────────────────────────────────

    [Fact]
    public void DeriveWorkspace_UsesGrandparent_OfConfigPath()
    {
        // <ws>/tools/rag/rag-config.yaml  →  workspace = <ws>
        var ws = Path.Combine(_tempDir, "ws");
        var configPath = Path.Combine(ws, "tools", "rag", "rag-config.yaml");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, "x: 1");

        var derived = RagConfig.DeriveWorkspace(configPath);

        Assert.Equal(Path.GetFullPath(ws), Path.GetFullPath(derived));
    }

    [Fact]
    public void DeriveWorkspace_FallsBackToEnvVar_WhenConfigPathNull()
    {
        var fakeWs = Path.Combine(_tempDir, "envws");
        Directory.CreateDirectory(fakeWs);
        try
        {
            Environment.SetEnvironmentVariable("RAG_WORKSPACE", fakeWs);
            var derived = RagConfig.DeriveWorkspace(null);
            Assert.Equal(fakeWs, derived);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RAG_WORKSPACE", null);
        }
    }

    [Fact]
    public void Load_SetsLoadedFromAndWorkspaceFromConfigPath()
    {
        var ws = Path.Combine(_tempDir, "ws2");
        var configPath = Path.Combine(ws, "tools", "rag", "rag-config.yaml");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, MinimalConfig());

        var cfg = RagConfig.Load(configPath);

        Assert.Equal(Path.GetFullPath(configPath), cfg.LoadedFrom);
        Assert.Equal(Path.GetFullPath(ws), Path.GetFullPath(cfg.Workspace));
        Assert.Equal(
            Path.Combine(Path.GetFullPath(ws), ".rag/manifest.json"),
            cfg.ManifestAbsPath);
    }

    [Fact]
    public void ResolveConfigPath_PrioritisesExplicitArg_OverEnv()
    {
        var explicitPath = Path.Combine(_tempDir, "explicit.yaml");
        File.WriteAllText(explicitPath, "x: 1");
        var envPath = Path.Combine(_tempDir, "env.yaml");
        File.WriteAllText(envPath, "x: 1");
        try
        {
            Environment.SetEnvironmentVariable("RAG_CONFIG", envPath);
            var resolved = RagConfig.ResolveConfigPath(explicitPath);
            Assert.Equal(Path.GetFullPath(explicitPath), resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RAG_CONFIG", null);
        }
    }

    [Fact]
    public void ResolveConfigPath_FallsThroughToEnv_WhenExplicitArgPathDoesNotExist()
    {
        // Simulates launchSettings --config path that is valid on the original machine
        // but doesn't exist on a different project/machine layout.
        var missingPath = Path.Combine(_tempDir, "nonexistent.yaml");  // NOT created
        var envPath = Path.Combine(_tempDir, "from-env-fallback.yaml");
        File.WriteAllText(envPath, "x: 1");
        try
        {
            Environment.SetEnvironmentVariable("RAG_CONFIG", envPath);
            Environment.SetEnvironmentVariable("RAG_WORKSPACE", null);
            var resolved = RagConfig.ResolveConfigPath(missingPath);
            Assert.Equal(Path.GetFullPath(envPath), resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RAG_CONFIG", null);
        }
    }

    [Fact]
    public void ResolveConfigPath_UsesRagConfigEnv_WhenNoExplicitArg()
    {
        var envPath = Path.Combine(_tempDir, "from-env.yaml");
        File.WriteAllText(envPath, "x: 1");
        try
        {
            Environment.SetEnvironmentVariable("RAG_CONFIG", envPath);
            Environment.SetEnvironmentVariable("RAG_WORKSPACE", null);
            var resolved = RagConfig.ResolveConfigPath(null);
            Assert.Equal(Path.GetFullPath(envPath), resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RAG_CONFIG", null);
        }
    }

    [Fact]
    public void ResolveConfigPath_DerivesFromRagWorkspace_WhenOnlyWorkspaceSet()
    {
        var ws = Path.Combine(_tempDir, "wsroot");
        var derivedConfig = Path.Combine(ws, "tools", "rag", "rag-config.yaml");
        Directory.CreateDirectory(Path.GetDirectoryName(derivedConfig)!);
        File.WriteAllText(derivedConfig, "x: 1");
        try
        {
            Environment.SetEnvironmentVariable("RAG_CONFIG", null);
            Environment.SetEnvironmentVariable("RAG_WORKSPACE", ws);
            var resolved = RagConfig.ResolveConfigPath(null);
            Assert.Equal(Path.GetFullPath(derivedConfig), resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RAG_WORKSPACE", null);
        }
    }

    [Fact]
    public void ResolveConfigPath_FallsBackToBaseDirectory_WhenNoEnvAndNoArg()
    {
        // No file required — we just check the *path* fallback.
        try
        {
            Environment.SetEnvironmentVariable("RAG_CONFIG", null);
            Environment.SetEnvironmentVariable("RAG_WORKSPACE", null);
            var resolved = RagConfig.ResolveConfigPath(null);
            Assert.Equal(Path.Combine(AppContext.BaseDirectory, "rag-config.yaml"), resolved);
        }
        finally
        {
            // already null — defensive
            Environment.SetEnvironmentVariable("RAG_CONFIG", null);
            Environment.SetEnvironmentVariable("RAG_WORKSPACE", null);
        }
    }
}
