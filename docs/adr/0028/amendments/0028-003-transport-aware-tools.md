# ADR-0028 Amendment 3: Transport-Aware Tools — `ListAdrs` Qdrant Source + `RagSession` Fix

## Date
2026-05-25

## Author
GitHub Copilot (design + implementation session)

## Summary

Five decisions that make the MCP tools transport-aware and eliminate hardcoded assumptions
about disk layout, ADR folder naming, and DI scoping.

---

## Deviation / Addition 1 — `RagSession.Collection` scoping bug in HTTP mode (D-6)

**Problem found during testing (root cause of all Qdrant tool failures):**

`RagSession` was `AddScoped` in HTTP mode. `RagSessionMiddleware` called `session.SetCollection`
on the *request-scope* instance. `ModelContextProtocol.AspNetCore` v1.3.0 creates a **child DI
scope** for tool invocation. The child scope constructs a fresh `RagSession` with
`Collection = cfg.Collection` (the YAML default). Every tool then queried the wrong collection.

`ListAdrs` was immune because it read `cfg.Workspace` from a singleton `RagConfig`, not
from `session.Collection`.

**Fix:** `RagSession.Collection` now reads lazily from `IHttpContextAccessor`:

```csharp
public string Collection
{
    get
    {
        var project = _http?.HttpContext?.Request.Query["project"]
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        if (project is not null) return project;

        var envCol = Environment.GetEnvironmentVariable("RAG_COLLECTION");
        if (!string.IsNullOrWhiteSpace(envCol)) return envCol;

        return _cfg.Collection;
    }
}
```

`IHttpContextAccessor` uses `AsyncLocal<HttpContext>` — always returns the live request
context regardless of which DI scope resolves `RagSession`. `RagSessionMiddleware` is removed.

| Mode  | `HttpContext`? | Result                                   |
|-------|----------------|------------------------------------------|
| STDIO | No             | falls back to env var / `cfg.Collection` |
| HTTP  | Yes            | reads `?project=` from current request   |

`RagSession` registered as `Singleton` in both STDIO and HTTP (middleware removed from HTTP path).

---

## Deviation / Addition 2 — `ListAdrsAsync` implemented in `QdrantDocumentStore` (D-1, D-4, D-5)

**Previous state:** `QdrantDocumentStore.ListAdrsAsync` returned `Array.Empty<AdrSummary>()`.
`RagTools.ListAdrs` bypassed the interface and scanned disk with a hardcoded `^\d{4}$` regex.

**New behaviour:**

- `QdrantDocumentStore.ListAdrsAsync` scrolls all chunk points where `adr_id != null`.
- Groups by `adr_id`. Per group:
  - `Title` — extracted from first chunk's `text` using a first-line H1 regex.
  - `MainFile` — `relPath` of the first chunk whose `doc_kind` matches the configured
    ADR doc kind (see Amendment 2 below).
  - `Amendments` — count of chunks whose `doc_kind` matches the configured amendment doc kind.
  - `Examples` — count of chunks whose `doc_kind = "adr_example"`.
- `CachedDocumentStore` already caches this under key `adrs:{collection}`.
- `RagTools.ListAdrs` calls `store.ListAdrsAsync(session.Collection)`. No disk access. No regex.

Works for **both STDIO and HTTP** — `session.Collection` is correct in both (via D-6 fix).

---

## Deviation / Addition 3 — `adr_doc_kind` / `amendment_doc_kind` from `metadata-rules.yaml` (D-2)

**Motivation:** Not every project uses ADRs. These settings must not pollute `rag-config.yaml`.
Ingest must not hardcode the string `"adr_main"` — the value comes from the repo's own
`metadata-rules.yaml`.

**Changes:**

`metadata-rules.yaml` gains an optional `adr:` section:

```yaml
adr:
  adr_doc_kind: "adr_main"          # required when adr: is present
  amendment_doc_kind: "adr_amendment" # optional; omit to suppress amendment counting
```

`MetadataRulesSection` gains an `Adr` sub-section:

```csharp
public sealed class MetadataRulesAdrSection
{
    public string? AdrDocKind       { get; init; }
    public string? AmendmentDocKind { get; init; }
}
```

`RagConfigPayload` gains two new nullable fields:

```csharp
public string? AdrDocKind       { get; set; }
public string? AmendmentDocKind { get; set; }
```

Written by ingest (`RagConfigPayload.From(cfg, ...)`), read by `ListAdrsAsync` at runtime.
Falls back to `"adr_main"` / `"adr_amendment"` if the stored config has nulls (old collections).

---

## Deviation / Addition 4 — `IContentSource` replaces disk-fallback `if`-chain in `ReadDocs` (D-3)

**Previous state:** `ReadDocs` contained `File.ReadAllTextAsync(cfg.Workspace / relPath)` as
a disk fallback with no transport check. In HTTP mode, the workspace may not be mounted.

**New abstraction:**

```csharp
public interface IContentSource
{
    Task<string?> ReadAsync(string collection, string relPath, CancellationToken ct);
}
```

Two implementations registered in `Program.cs` based on `MCP_TRANSPORT`:

| Transport | Implementation        | Behaviour                                             |
|-----------|-----------------------|-------------------------------------------------------|
| `stdio`   | `DiskContentSource`   | reads `cfg.Workspace / relPath` from disk             |
| `http`    | `QdrantContentSource` | calls `store.FetchContentAsync(collection, relPath)` |

`RagTools` receives `IContentSource` as a constructor parameter. No `if (isHttp)` inside
the tool — the transport decision lives in `Program.cs`.

---

## Files changed

| Project           | Files                                                                              |
|-------------------|------------------------------------------------------------------------------------|
| `RagTools.Core`   | `RagSession.cs`, `MetadataRulesSection` (in `RagConfig.cs`), `RagConfigPayload.cs`, `QdrantDocumentStore.cs` |
| `RagTools.Mcp`    | `Program.cs`, `IContentSource.cs` (new), `DiskContentSource.cs` (new), `QdrantContentSource.cs` (new), `Tools/RagTools.cs`, `Middleware/RagSessionMiddleware.cs` (removed) |
| Config            | `tools/rag-dotnet/metadata-rules.yaml` — add `adr:` section                       |
