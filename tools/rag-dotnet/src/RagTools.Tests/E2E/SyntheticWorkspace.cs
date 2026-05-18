using RagTools.Core;

namespace RagTools.Tests.E2E;

/// <summary>
/// Creates a fully self-contained temporary workspace with synthetic markdown docs and a
/// config.yaml that points at a caller-supplied Qdrant URL and collection name.
///
/// The workspace is repo-independent: it contains no references to ECommerceApp ADR numbers,
/// titles, or domain concepts. Any project that follows the docs/adr/NNNN/ convention will
/// produce the same structure.
///
/// Layout:
///   &lt;root&gt;/
///     tools/rag/config.yaml
///     docs/
///       concepts/
///         alpha.md   — describes the "Alpha" pattern (value-object style)
///         beta.md    — describes the "Beta" pattern (CQRS-style query/command split)
///       adr/
///         0001/
///           0001-alpha.md        — ADR that adopts Alpha pattern
///           README.md            — router stub
///           amendments/
///             a1-alpha-ext.md  — amendment extending Alpha to collections
///         0002/
///           0002-beta.md         — ADR that adopts Beta pattern
///           README.md            — router stub
/// </summary>
public sealed class SyntheticWorkspace : IDisposable
{
    public string Root { get; }
    public string ConfigPath { get; }

    private SyntheticWorkspace(string root, string configPath)
    {
        Root = root;
        ConfigPath = configPath;
    }

    public static SyntheticWorkspace Create(string qdrantUrl, string collection, string modelDir)
    {
        var root = Path.Combine(Path.GetTempPath(), $"rag-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        // ── tools/rag/config.yaml ──────────────────────────────────────────
        var ragDir = Path.Combine(root, "tools", "rag");
        Directory.CreateDirectory(ragDir);

        var configPath = Path.Combine(ragDir, "config.yaml");
        File.WriteAllText(configPath, $"""
            source:
              roots:
                - docs
              exclude_globs: []
            embedder:
              model: "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2"
              dimensions: 384
              device: cpu
              batch_size: 32
              model_dir: "{modelDir.Replace('\\', '/')}"
            chunker:
              max_tokens: 800
              min_tokens: 1
              overlap_tokens: 80
              split_on_headings: [1, 2, 3]
            vector_store:
              backend: qdrant
              collection: "{collection}"
              url: "{qdrantUrl}"
            ranking:
              weights: []
            query:
              top_k: 5
              score_threshold: 0.0
            storage:
              manifest_path: ".rag/manifest.json"
            """);

        // metadata-rules.yaml: ADR ID patterns and doc_kind rules.
        // Without this, cfg.DetectAdrId() always returns null and GetAdrHistory finds nothing.
        File.WriteAllText(Path.Combine(ragDir, "metadata-rules.yaml"), """
            adr_id_patterns:
              - pattern: "adr/(?<id>\\d{4})/"
            doc_kind_rules:
              - glob: "**/amendments/**"
                kind: "adr_amendment"
              - glob: "docs/adr/**/README.md"
                kind: "adr_router"
              - glob: "docs/adr/**"
                kind: "adr_main"
              - glob: "docs/concepts/**"
                kind: "pattern"
            """);

        // ── docs/concepts/alpha.md ─────────────────────────────────────────
        var conceptsDir = Path.Combine(root, "docs", "concepts");
        Directory.CreateDirectory(conceptsDir);

        File.WriteAllText(Path.Combine(conceptsDir, "alpha.md"), """
            # Alpha Pattern

            The Alpha pattern is a domain modelling technique for representing identifiers
            as strongly-typed value objects instead of primitive types such as int or Guid.

            ## Motivation

            Using raw primitives for identifiers leads to accidental substitution errors:
            a function expecting a `CustomerId` can silently accept an `OrderId` because
            both are plain `Guid` at runtime.  The Alpha pattern wraps each ID type in its
            own record so the compiler enforces correctness.

            ## Structure

            ```csharp
            public readonly record struct CustomerId(Guid Value);
            public readonly record struct OrderId(Guid Value);
            ```

            ## Benefits

            - Compile-time safety: mixing ID types is a build error.
            - Readability: method signatures self-document which entity they accept.
            - Serialisation: custom converters handle JSON / EF Core mapping once.
            """);

        File.WriteAllText(Path.Combine(conceptsDir, "beta.md"), """
            # Beta Pattern

            The Beta pattern separates read and write responsibilities by routing all
            state-mutating operations through Commands and all data-retrieval operations
            through Queries.  This is often called CQRS (Command Query Responsibility
            Segregation) in the literature.

            ## Commands

            A Command expresses intent to change system state.  It carries only the data
            required to perform the action and returns nothing (or a result type when
            acknowledgement is needed).

            ## Queries

            A Query retrieves data without side effects.  Queries may be satisfied from
            a read-optimised projection or directly from the write model depending on
            consistency requirements.

            ## Benefits

            - Independent scaling of read and write paths.
            - Simplified query models: no domain logic in read projections.
            - Auditability: every state change is an explicit, named Command.
            """);

        // ── docs/adr/0001/ ─────────────────────────────────────────────────
        var adr0001 = Path.Combine(root, "docs", "adr", "0001");
        var adr0001Amendments = Path.Combine(adr0001, "amendments");
        Directory.CreateDirectory(adr0001Amendments);

        File.WriteAllText(Path.Combine(adr0001, "0001-alpha.md"), """
            # ADR-0001 — Adopt Alpha Pattern for Entity Identifiers

            ## Status
            Accepted

            ## Context
            The codebase was using raw `Guid` for all entity identifiers, causing frequent
            mix-ups between `CustomerId`, `OrderId`, and `ProductId` in service method
            signatures.

            ## Decision
            Adopt the Alpha pattern: every aggregate root identifier is a `readonly record struct`
            wrapping a `Guid`.  The compiler enforces correct usage at all call sites.

            ## Consequences
            - All repositories and services must update their signatures.
            - EF Core value converters must be registered for each ID type.
            - JSON serialisation requires custom converters.
            """);

        File.WriteAllText(Path.Combine(adr0001, "README.md"), """
            # ADR-0001 — Alpha Pattern

            Router: see [0001-alpha.md](0001-alpha.md) for the decision record.
            """);

        File.WriteAllText(Path.Combine(adr0001Amendments, "a1-alpha-ext.md"), """
            # Amendment A1 — Extend Alpha Pattern to Collection Identifiers

            ## Status
            Accepted (amendment to ADR-0001)

            ## Change
            Apply the Alpha pattern not only to aggregate root IDs but also to
            collection-level identifiers (e.g. `CartLineId`, `InvoiceLineId`).
            The original decision was scoped to aggregate roots only; this amendment
            broadens the rule to child entities with their own identity within a
            bounded context.
            """);

        // ── docs/adr/0002/ ─────────────────────────────────────────────────
        var adr0002 = Path.Combine(root, "docs", "adr", "0002");
        Directory.CreateDirectory(adr0002);

        File.WriteAllText(Path.Combine(adr0002, "0002-beta.md"), """
            # ADR-0002 — Adopt Beta Pattern (CQRS) for Application Layer

            ## Status
            Accepted

            ## Context
            The application layer contained service classes that mixed query logic with
            command logic, making it difficult to reason about side effects and to scale
            reads independently of writes.

            ## Decision
            Adopt the Beta pattern across all bounded contexts:
            - Commands are handled by `ICommandHandler<TCommand>` implementations.
            - Queries are handled by `IQueryHandler<TQuery, TResult>` implementations.
            - No service class may contain both a command handler and a query handler.

            ## Consequences
            - Clear ownership: each handler file has a single responsibility.
            - Independent testing: command handlers can be tested without query infrastructure.
            - Overhead: more files per feature.
            """);

        File.WriteAllText(Path.Combine(adr0002, "README.md"), """
            # ADR-0002 — Beta Pattern

            Router: see [0002-beta.md](0002-beta.md) for the decision record.
            """);

        return new SyntheticWorkspace(root, configPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch { /* best-effort cleanup */ }
    }
}
