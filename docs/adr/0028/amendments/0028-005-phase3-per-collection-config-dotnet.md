# ADR-0028 Amendment 5: Phase 3 Per-Collection Config Persistence — .NET Implementation

## Date
2026-05-28

## Author
GitHub Copilot (Phase 3 implementation session)

## Summary

Phase 3 from the roadmap (`docs/roadmap/rag-remote-multitenant.md` §Phase 3) has shipped
on the .NET server. The "config travels with the docs" model promised in the main ADR
and documented as a gap in [Amendment 004](./0028-004-per-collection-config-gap.md) is
now live for the HTTP path. The caveat at the end of Amendment 004 is hereby retired —
within the scope listed below.

This amendment is **descriptive**: it records what landed, the deviations from the
roadmap design notes, and what is intentionally still mounted-only. It does not change
the original decision in ADR-0028 itself.

Python parity is tracked separately (roadmap Phase 3 P3-7) and is **not** covered here.

---

## Branch and commits

Branch: `feat/rag-phase3-per-collection-config`

| Commit | Step | Scope |
| --- | --- | --- |
| `b3d11ef3` | P3-0 | `RagConfigPayload.Weights` field added; `IConfigSource` abstraction stub |
| `ad518104` | P3-1 + P3-5 | `FileConfigSource`, `QdrantConfigSource`, `LayeredConfigSource`, `CachingConfigSource`; DI mode-switch via `RAG_CONFIG_SOURCE` |
| `edc87397` | P3-2 + P3-4 | `RagQueryService` / `RagReadDocsService` consume `IConfigSource`; `BatchIngestService` persists `RagConfigPayload` |
| `e9f207ef` | P3-3 Design A | Glossary as English allow-list filter (subsequently replaced) |
| `ccd53f1a` | P3-3 Design B | Pivot to per-collection `GlossaryEntries` (English → patterns); `RAG_GLOSSARY_FALLBACK` mode-switch (`mounted` \| `none`); two preprocessors: `MountedFallbackGlossaryExpansionPreprocessor`, `DbOnlyGlossaryExpansionPreprocessor` |
| `6413b968` | P3-3b | `MarkdownChunker` overload + `DocumentProcessor` resolves `MaxTokens` / `OverlapTokens` from per-collection payload; `LengthTruncationPreprocessor` resolves the same on the query path |
| `7cf09880` | P3-3c | `DocumentProcessor` resolves ranking weights from `payload.Weights`; `RankingWeightResolver` gains a `(weights, stubByteThreshold)` overload |

All commits keep the 525-test suite green.

---

## What is per-collection now (HTTP path)

Resolved per-request from `IConfigSource.GetEffectiveAsync(collection, ct)`:

- `MaxTokens` — used by `MarkdownChunker` (per document during ingest) and by
  `LengthTruncationPreprocessor` (per query).
- `OverlapTokens` — used by `MarkdownChunker`.
- `Weights` — used by `DocumentProcessor` via `RankingWeightResolver.Resolve(...,
  payload.Weights, cfg.Ranking.StubByteThreshold)`.
- `GlossaryEntries` — used by `MountedFallbackGlossaryExpansionPreprocessor` (per query;
  empty list → mounted fallback) or `DbOnlyGlossaryExpansionPreprocessor` (per query;
  empty list → identity / no expansion). Selection driven by `RAG_GLOSSARY_FALLBACK`.
- `ScoreThreshold`, `FetchK` — already resolved per-collection in `RagQueryService` /
  `RagReadDocsService` since P3-2.
- `HistoryField`, `AdrDocKind`, `AmendmentDocKind` — persisted in payload, used by
  `RagHistoryService` and chunk classification paths (orphan fixes remain — see below).

Schema bumped: `RagConfigPayload.SchemaVersion = 2` (was 1). The bump replaces the earlier
`GlossaryTerms : List<string>` allow-list with `GlossaryEntries : List<GlossaryEntry>`
verbatim — no field merging, override-wins semantics in
`RagConfigPayloadExtensions.Merge`.

---

## What is intentionally still mounted-only

- **`StubByteThreshold`** (used by the `/example-implementation/` weight rule). Not
  persisted in `RagConfigPayload`. Persisting would require a schema bump with no current
  multi-tenant use case (one threshold suits every project so far).
- **Length-truncation on the Ingest path.** `LengthTruncationPreprocessor` uses the
  mounted default when `EmbedPurpose.Ingest`. Rationale: `IngestWorker` is a
  `BackgroundService` — when it dequeues a job there is no `HttpContext`, so the ambient
  `HttpCollectionResolver` would fail. The chunker already enforces per-collection
  `MaxTokens` upstream, so truncation here is a safety net only. Documented in a one-line
  code comment on the preprocessor.
- **`MinTokens`** (chunker). Not persisted in `RagConfigPayload`.
- **CLI ingestor (`RagTools.Ingest/Program.cs`)** wires a `FileConfigSource`. The offline
  tool does not write to a Qdrant `__config__` point; the HTTP `BatchIngestService` does
  that on ZIP upload.

---

## Architecture summary

```
HTTP request → HttpCollectionResolver (reads ?project=) → RagSession.Collection
                              │
                              ▼
                       IConfigSource
                  (mode-switch via RAG_CONFIG_SOURCE)
                  ┌───────────────────────────────────┐
                  │ FileConfigSource     — mounted    │
                  │ QdrantConfigSource   — Qdrant     │
                  │ LayeredConfigSource  — Qdrant +   │
                  │                       mounted     │
                  └───────────────────────────────────┘
                              │ wrapped by CachingConfigSource
                              ▼
                    RagConfigPayload { MaxTokens, OverlapTokens,
                                       Weights, GlossaryEntries,
                                       ScoreThreshold, FetchK,
                                       HistoryField, AdrDocKind,
                                       AmendmentDocKind, ... }
                              │
        ┌─────────────────────┼──────────────────────────────────────┐
        ▼                     ▼                                      ▼
DocumentProcessor      LengthTruncationPreprocessor          Glossary preprocessor
(chunker overload      (query path uses payload;             (Mounted-Fallback or
 + weight resolver)     ingest path uses mounted)             DbOnly per env mode)
```

The ingest path passes the resolved collection on the `DocumentProcessingRequest` rather
than reading ambient `RagSession`, because `IngestWorker` runs without `HttpContext`.

---

## Glossary design — A → B pivot

Design A (allow-list filter on a server-mounted glossary) was implemented in `e9f207ef`
and then replaced in `ccd53f1a` because it could not represent a per-collection glossary
whose entries did not already exist on the server. The shipped Design B stores the full
glossary in the payload and merges with `override-wins` semantics:

| `RAG_GLOSSARY_FALLBACK` | Empty `payload.GlossaryEntries` behaviour |
| --- | --- |
| `mounted` (default) | Use mounted `multilingual-glossary.yaml` |
| `none` | Skip expansion entirely (identity) |

The preprocessor type is resolved once at startup by `ResolveGlossaryPreprocessorType`
in `Program.cs` and selected via the non-generic `EmbedderPipelineBuilder.WithPreprocessor(Type)`
overload added in the same commit. Unknown values throw at startup so misconfiguration
fails fast.

---

## Audit-bundle origin

P3-3b and P3-3c were not on the original roadmap. They were found in an audit prompted
by the user immediately after P3-3 shipped: *"please check for other cases when we by
accident mount local instead of values from db ok? but only for HTTP"*. The audit found
three more sites:

1. `MarkdownChunker` was a singleton holding mounted `MaxTokens` / `OverlapTokens` in
   private fields (P3-3b).
2. `LengthTruncationPreprocessor` was a singleton holding the mounted `MaxTokens` (P3-3b).
3. `DocumentProcessor` called `RankingWeightResolver.Resolve(path, size, cfg.Ranking)`
   directly (P3-3c).

All three are now fixed.

---

## Known orphans (not part of this amendment)

- **P3-X** — *Resolved 2026-05-28 in a follow-up commit on the same branch:*
  `RagHistoryService` now resolves `HistoryField` via `IConfigSource.GetEffectiveAsync`
  instead of calling `IDocumentStore.FetchConfigAsync` directly. The change brings the
  per-collection lookup through `CachingConfigSource` and respects the active mode-switch
  (`FileConfigSource` / `QdrantConfigSource` / `LayeredConfigSource`).
- **~~P3-Y~~ (withdrawn)** — Earlier draft listed `AdrDocKind` / `AmendmentDocKind` as
  orphan. Re-audit: they are read on the HTTP path by
  `QdrantDocumentStore.ListAdrsAsync` (directly via `FetchConfigAsync`, intentionally
  uncached because `list_adrs` is a low-traffic orientation tool), and ingest-time chunk
  classification uses the per-batch `MetadataRules` passed through
  `DocumentProcessingRequest.DocKindOverride` from `BatchIngestService`. No orphan.

Both notes are tracked in `/memories/repo/rag-mcp-anomalies.md`.

---

## Retirement of the Amendment 004 caveat

Amendment 004 ended with:

> This caveat will be removed once Phase 3 P3-6 (ADR update) is complete.

The caveat is retired **for the .NET HTTP server only**. The Python server still
operates per the original gap description — see roadmap Phase 3 P3-7. Amendment 004
will be edited in a separate commit to point at this amendment.

---

## References

- Roadmap: [`docs/roadmap/rag-remote-multitenant.md` § Phase 3](../../../roadmap/rag-remote-multitenant.md#phase-3--per-collection-config-persistence-gap-fix)
- Original gap: [Amendment 004](./0028-004-per-collection-config-gap.md)
- Tech details (will be updated separately): [`tech-details-dotnet.md`](../tech-details-dotnet.md)
- Anomalies log: `/memories/repo/rag-mcp-anomalies.md`
