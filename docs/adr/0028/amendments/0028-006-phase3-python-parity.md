# ADR-0028 Amendment 6: Phase 3 Per-Collection Config — Python Parity (P3-7)

## Date
2026-05-29

## Author
GitHub Copilot (Phase 3 Python implementation session)

## Summary

Completes the Python mirror of the .NET Phase 3 per-collection config persistence
shipped in [Amendment 005](./0028-005-phase3-per-collection-config-dotnet.md). The
"config travels with the docs" model is now live for the Python MCP server's HTTP
transport. Full technical details in [`tech-details-python.md § Phase 3`](../tech-details-python.md#phase-3--per-collection-config-persistence).

This amendment is **descriptive** — it records what landed, key design decisions made
during the Python implementation, and deviations or divergences from the .NET design.

---

## Branch and commits

Branch: `feat/rag-phase3-per-collection-config`

| Commit | Slice | Scope |
|---|---|---|
| `ef0583a4` | P3-7a | `IConfigSource` abstraction + `config/` package (`payload.py`, `sources.py`, `bootstrap.py`); `storage/document_store.py` `store_config` / `get_config` |
| `f2fd677e` | P3-7b | `ingest_routes.py` persists `__config__` point; `ensure_collection` regression fix; `Dockerfile` copies `config/` package |
| `da7abc3c` | P3-7c | Per-collection glossary read-path: `EmbedContext` override, `GlossaryExpansionPreprocessor`, `QueryEngine.search`, `_resolve_glossary_entries`, `state.CONFIG_SOURCE`, ingest bake + cache invalidation; 13 new unit tests |

All commits keep the 381-test suite green.

---

## What is per-collection now (Python HTTP path)

Resolved per-call from `IConfigSource.get_effective(collection)`:

- **`GlossaryEntries`** — used by `GlossaryExpansionPreprocessor` at query time. The
  preprocessor checks `ctx.glossary_entries`:
  - `None` → fall back to mounted `multilingual-glossary.yaml` (STDIO / file mode, or
    collection loaded before P3-7a shipped).
  - `()` → empty tuple — collection has no glossary, suppresses mounted entirely.
  - Non-empty tuple → use per-collection entries verbatim.
- **`Weights`** — stored in `RagConfigPayload.weights`; not yet wired to the ranking
  path on Python (deferred to a future slice — Python ranking weight resolver is
  separate from the .NET `RankingWeightResolver`).
- **`MaxTokens` / `OverlapTokens`** — stored in payload for parity; not yet consumed
  by the Python chunker (single-collection for now).
- **`ScoreThreshold`, `FetchK`, `HistoryField`, `AdrDocKind`, `AmendmentDocKind`** —
  already wired to per-collection reads through `QdrantDocumentStore.get_config` (since
  Phase 2); now flowing through `IConfigSource` layer.

---

## Key design decisions

### Decision 1: No `RAG_INGEST_BAKE_GLOSSARY` env var

**Options considered:**

| Option | Description |
|---|---|
| A (chosen) | Always bake mounted glossary at ingest time; ZIP-supplied glossary wins if present. No env var. |
| B | Add `RAG_INGEST_BAKE_GLOSSARY=true/false` env var to opt in/out of baking. |

**Decision**: Option A — strict parity with .NET `BatchIngestService.BuildEffectivePayload`
which always calls `MultilingualGlossary.Load(cfg.GlossaryPath)` with no opt-out flag.
The env var added complexity for no benefit in the current single-tenant deployment; it
can be added later if a multi-tenant deployment needs to serve collections that must NOT
inherit the mounted glossary.

### Decision 2: No `DbOnlyGlossaryExpansionPreprocessor` variant on Python

.NET ships two preprocessor variants selected by `RAG_GLOSSARY_FALLBACK`:
- `MountedFallbackGlossaryExpansionPreprocessor` — empty payload → use mounted.
- `DbOnlyGlossaryExpansionPreprocessor` — empty payload → identity (no expansion).

Python uses a single `GlossaryExpansionPreprocessor` whose `process()` method reads
`ctx.glossary_entries` at call time. The `None` sentinel (returned when
`CONFIG_SOURCE is None`) gives the same behaviour as `mounted` mode; an empty tuple `()`
gives the same behaviour as `none` mode. Selection is implicit from the call context
rather than startup DI — simpler for an asyncio single-process server.

If `RAG_GLOSSARY_FALLBACK` semantics are needed on Python in the future, the preprocessor
can be extended to read an env var in its constructor with no protocol change.

### Decision 3: `glossary_entries=None` vs `glossary_entries=()` are semantically distinct

`None` means "no override — use mounted". `()` (empty tuple) means "this collection has
no glossary — suppress mounted". Collections loaded before P3-7a existed return `None`
from `_resolve_glossary_entries` (because `CONFIG_SOURCE is None` in STDIO, or
`get_effective` returns a payload with empty `glossary_entries` from the mounted
`FileConfigSource`) — both collapse to mounted fallback, preserving backward compat.

### Decision 4: `_tool_get_history` not updated

`_tool_get_history` synthesizes a `"history {id}"` query string internally. Glossary
expansion on this string is not meaningful (the expansion targets natural-language
multilingual queries, not synthetic keys). The tool was intentionally left without
`_resolve_glossary_entries` — it passes `glossary_entries=None` implicitly (missing kwarg
→ default `None` in `QueryEngine.search`).

---

## Deviations from .NET Amendment 005

| .NET | Python |
|---|---|
| `RAG_GLOSSARY_FALLBACK` selects preprocessor type at DI startup | Single preprocessor; `None`/`()` sentinel distinguishes mounted vs suppress |
| `LengthTruncationPreprocessor` resolves `MaxTokens` from `payload` at query time | Python `LengthTruncationPreprocessor` does not yet read per-collection `MaxTokens` (deferred) |
| `RankingWeightResolver.Resolve(path, size, payload.Weights)` | Python weight resolver not yet wired (deferred) |
| `RAG_CONFIG_SOURCE` env var selects `FileConfigSource` / `QdrantConfigSource` / `LayeredConfigSource` | Identical env var and logic in `config/bootstrap.py` |
| `CachingConfigSource` (IDistributedCache + IMemoryCache + SemaphoreSlim) | `CachingConfigSource` (`asyncio.Lock` + `dict` TTL + LRU counter) |

---

## What is intentionally still mounted-only (Python)

- **`Weights`** — stored in payload; not yet consumed by Python ranking path.
- **`MaxTokens` / `OverlapTokens`** — stored in payload; Python chunker is always `MarkdownChunker`
  using mounted config (no per-collection chunker override yet).
- **`MinTokens`** — not persisted in `RagConfigPayload` on either server.
- **CLI ingestor (`ingest.py`)** — `build_config_source` is not called; the CLI tool
  does NOT write a `__config__` point when run stand-alone. The HTTP server path
  (`_parse_zip_batch`) writes it. Parity with .NET CLI behaviour.

---

## STDIO safety

The STDIO entrypoint (`mcp_server.py __main__` branch via `asyncio.run(_run_stdio())`)
never calls `build_config_source`. Therefore:
- `state.CONFIG_SOURCE` stays `None`.
- `_resolve_glossary_entries` short-circuits and returns `None`.
- `GlossaryExpansionPreprocessor` falls back to `self._glossary` (loaded at startup from
  mounted YAML).
- Embed result is byte-identical to the pre-P3-7 STDIO behaviour.

---

## Test coverage added (P3-7c)

13 new unit tests in `tools/rag/tests/test_p3_7c_per_collection_glossary.py`:

| Class | Tests |
|---|---|
| `TestGlossaryOverrideInContext` | Uses ctx override; falls back to mounted when `None`; empty override suppresses mounted; ingest purpose skips expansion; singletons have `None` default |
| `TestResolveGlossaryEntries` | Returns `None` when `CONFIG_SOURCE is None`; returns entries from active source; returns `None` on exception |
| `TestIngestRoutesBakesGlossary` | No ZIP + no mounted → empty; no ZIP + mounted → bakes; ZIP wins; `invalidate` called after `store_config` |

Full suite: 381 passed.

---

## Retirement of Amendment 004 caveat (Python portion)

[Amendment 004](./0028-004-per-collection-config-gap.md) documented the gap for both
servers. Amendment 005 retired the caveat for .NET. This amendment retires it for the
Python HTTP path.

The caveat is **not** retired for:
- Python STDIO path (intentionally; STDIO is single-collection by design).
- Python ranking weights (deferred — `Weights` field stored but not consumed).
- Python per-collection chunker settings (deferred).

---

## References

- Roadmap: [`docs/roadmap/rag-remote-multitenant.md` § Phase 3](../../../roadmap/rag-remote-multitenant.md#phase-3--per-collection-config-persistence-gap-fix)
- .NET parity: [Amendment 005](./0028-005-phase3-per-collection-config-dotnet.md)
- Python tech details: [`tech-details-python.md § Phase 3`](../tech-details-python.md#phase-3--per-collection-config-persistence)
- Original gap: [Amendment 004](./0028-004-per-collection-config-gap.md)
