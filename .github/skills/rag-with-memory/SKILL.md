---
name: rag-with-memory
description: >
  Cache RAG knowledge into context-mode's FTS5 session memory so subsequent
  recalls cost zero RAG re-bill. Use when the same ADR/BC/area docs will be
  read 3+ times in a session (long debug, plan+implement+verify, multi-step
  refactor). Skip for one-shot questions — direct RAG is cheaper.
argument-hint: "[ADR-NNNN | BC name | topic]"
---

# RAG ↔ context-mode handoff — preferred (L2) + manual (L1) fallback

> Canonical rules: [`.github/instructions/mcp-routing.instructions.md` — RAG ↔ context-mode handoff section](../../instructions/mcp-routing.instructions.md). This skill is a step-by-step walkthrough.
>
> **L2 (preferred, both RAG servers)**: `query_docs_cached` — single call returns formatted markdown + deterministic source label. Cache step is a pass-through `ctx_index`.
> **L1 (manual fallback)**: 3-step handoff. Use only when L2 times out or returns an error.

## Goal

Read project knowledge from RAG **once**, persist it into context-mode's FTS5 store, then recall any number of times via local BM25 search with **zero RAG re-bill** and **zero re-embedding**.

## When to use this skill

- A long debugging or implementation session referencing the same ADR / BC / area.
- A planner/implementer/verifier handoff where all three agents need the same docs.
- You're about to call `query_docs` for the **second time** with similar keywords.
- The user said "we'll be working on X today / for the next hour".

## When NOT to use this skill

- One-shot question (RAG once, never re-read) — extra `ctx_index` call is overhead.
- Pure code edit with no knowledge lookup.
- The content is already cached this session (`ctx_search source="rag-cache-..."` first).

## Three similar "memory" surfaces — pick the right one

Three near-identical systems exist. **The handoff uses only the third.** Empirical POC (2026-05-27) showed weaker models picked the wrong one in all 3 steps when no rule named all three explicitly. Do not repeat that mistake.

| Surface | What it is | Use here? |
|---|---|---|
| `semantic_search` | VS Code's embedded workspace search (grep with embeddings). Not our RAG MCP. | ❌ NO — for step 1 use `query_docs` / `read_docs` / `get_history` |
| `memory.create` / `memory.view` (paths `/memories/*`) | VS Code's persistent notes for cross-session preferences. Single-doc, no FTS. | ❌ NO — not searchable for handoff recalls |
| `ctx_index` / `ctx_search` | context-mode's FTS5 knowledge base (Porter+trigram+RRF). Markdown-aware chunking. | ✅ YES — the only correct cache |

---

## Step 0 (one-shot per session) — discover the workspace mount

The context-mode container mounts the repo at a parametric path (default `/workspace`, overridable via `CONTEXT_MODE_WORKSPACE` in `.env.context-mode`). The mount is exposed as an env var inside the container so agents can probe it instead of hardcoding the path.

If this session will also call `ctx_execute_file(path)` (i.e. you'll pass absolute filesystem paths into the sandbox), run **once** at session start and cache mentally:

```
ctx_execute("shell", "echo $CONTEXT_MODE_WORKSPACE")
# → /workspace          (default)
# → /repo               (forked compose override, hypothetical)
```

Then use the returned value as the prefix for every absolute path. Pure `ctx_index` / `ctx_search` handoff (the common case) does NOT need this probe — those tools take `content=` / `queries=` strings, not paths.

---

## Preferred flow (L2) — `query_docs_cached`

Use this on either RAG server (`ecommerceapp-rag-python` or `ecommerceapp-rag-dotnet`). It collapses steps 1+2 of the manual flow into a single call + a pass-through.

```
  ┌──────────────────────────────────────────────────────────────┐
  │  STEP 1 — Single RAG call returns markdown + label           │
  ├──────────────────────────────────────────────────────────────┤
  │   resp = query_docs_cached(                                  │
  │     question="...", bc=?, top_files=3                        │
  │   )                                                          │
  │   → { source: "rag-cache-adr0028-<hash8>",                   │
  │       markdown: "# ...\n\n## file.md\n**Path**: ...",        │
  │       files_count, chunks_count, next_step }                 │
  └──────────────────────────────────────────────────────────────┘
                        ↓
  ┌──────────────────────────────────────────────────────────────┐
  │  STEP 2 — Pass-through ctx_index                             │
  ├──────────────────────────────────────────────────────────────┤
  │   ctx_index(content=resp.markdown, source=resp.source)       │
  └──────────────────────────────────────────────────────────────┘
                        ↓
  ┌──────────────────────────────────────────────────────────────┐
  │  STEP 3 — Recall (identical to L1)                           │
  ├──────────────────────────────────────────────────────────────┤
  │   ctx_search(queries=[...], source="rag-cache-")             │
  └──────────────────────────────────────────────────────────────┘
```

The wrapper:
- Picks `query_docs` semantics with the same ranking and `bc=` filter.
- Groups hits per file (top 3 by default), keeps top 5 chunks per file.
- Renders the template below for you — same shape as L1 so L1 and L2 caches interoperate.
- Derives `source` deterministically: `rag-cache-adr<NNNN>-<hash8>` if the question mentions an ADR id, `rag-cache-<slug(bc)>-<hash8>` if `bc=` is set, else `rag-cache-q-<hash8>`. Same `(question, bc)` → same `source` → idempotent overwrite.

If `query_docs_cached` is not available (e.g. it returns an error or the active server pre-dates Phase 7), fall through to the manual L1 flow below.

---

## Manual flow (L1) — fallback

```
  ┌──────────────────────────────────────────────────────────────┐
  │  STEP 1 — Read from RAG once                                 │
  ├──────────────────────────────────────────────────────────────┤
  │                                                              │
  │   query_docs(query="...", bc?, top_k=5)                      │
  │        ↓ (if you need full file bodies)                      │
  │   read_docs(query="...", top_files=3)                        │
  │        ↓                                                     │
  │   format the chunks into one markdown doc                    │
  │   (template below)                                           │
  │                                                              │
  └──────────────────────────────────────────────────────────────┘
                        ↓
  ┌──────────────────────────────────────────────────────────────┐
  │  STEP 2 — Cache into context-mode (one call)                 │
  ├──────────────────────────────────────────────────────────────┤
  │                                                              │
  │   ctx_index(                                                 │
  │     content="<the markdown from step 1>",                    │
  │     source="rag-cache-<scope>-<topic>"                       │
  │   )                                                          │
  │                                                              │
  │   → returns: { chunks_indexed: N, source: "..." }            │
  │                                                              │
  └──────────────────────────────────────────────────────────────┘
                        ↓
  ┌──────────────────────────────────────────────────────────────┐
  │  STEP 3 — Recall any number of times (zero RAG re-bill)      │
  ├──────────────────────────────────────────────────────────────┤
  │                                                              │
  │   ctx_search(                                                │
  │     queries=["specific question", "another angle"],          │
  │     source="rag-cache-<scope>"   ← partial-match works       │
  │   )                                                          │
  │                                                              │
  └──────────────────────────────────────────────────────────────┘
```

## Source naming convention (mandatory)

Always lowercase, kebab-case, ASCII. Always prefixed with `rag-cache-` so all RAG caches are recallable via `source="rag-cache"`.

| Scope | Pattern | Example |
|---|---|---|
| Specific ADR | `rag-cache-adr<NNNN>-<topic>` | `rag-cache-adr0028-ragsession-icontentsource` |
| Bounded context | `rag-cache-<bc>-<topic>` | `rag-cache-orders-checkout-rules` |
| Cross-cutting area | `rag-cache-<area>-<topic>` | `rag-cache-validation-fluentvalidation` |
| Roadmap slice | `rag-cache-roadmap-<bc>` | `rag-cache-roadmap-iam-atomic-switch` |
| Known issue | `rag-cache-ki<NNN>` | `rag-cache-ki008` |

## Markdown template (use exactly)

```markdown
# <Topic title>

> Cached from RAG on <YYYY-MM-DD>. Source: query_docs("<original query>"[, bc="<BC>"]).
> Refresh: re-run query_docs and call ctx_index with the same source label to overwrite.

## <First file or chunk title>

**Path**: `relative/path.md#Lstart-Lend`
**Breadcrumb**: <breadcrumb from RAG result>

<chunk body — keep code blocks, tables, lists intact>

## <Second file or chunk title>
...
```

Preserving the markdown structure (H2 per chunk, fenced code blocks, tables) matters: context-mode chunks markdown-aware. Break the structure and you break the ranking.

---

## Worked example — caching ADR-0028

### Step 1 — RAG once

```
get_history(id="0028")
# or, when you don't know the ID:
query_docs(query="RagSession scoping IContentSource ApiKey middleware", top_k=5)
```

Format the relevant chunks:

```markdown
# ADR-0028 — RagSession, IContentSource, ApiKey middleware

> Cached from RAG on 2026-05-27. Source: get_history(id="0028").

## RagSession scoping

**Path**: `docs/adr/0028/0028-rag-remote-multitenant.md#L120-L160`
**Breadcrumb**: ADR-0028 > Decision > RagSession

A `RagSession` is scoped to one tenant and one auth principal. ...

## IContentSource abstraction

**Path**: `docs/adr/0028/0028-rag-remote-multitenant.md#L161-L210`
**Breadcrumb**: ADR-0028 > Decision > IContentSource

The content store is hidden behind `IContentSource` so the per-tenant
implementation can be swapped. ...
```

### Step 2 — Cache once

```
ctx_index(
  content="<the markdown above>",
  source="rag-cache-adr0028-ragsession-icontentsource"
)
```

### Step 3 — Recall, recall, recall (no RAG bill)

```
ctx_search(
  queries=["how is RagSession isolated per tenant"],
  source="rag-cache-adr0028"
)

# 30 min later, different question, same cache:
ctx_search(
  queries=["IContentSource swap strategy"],
  source="rag-cache-adr0028"
)
```

---

## Trigger heuristics

| Signal in the conversation | Action |
|---|---|
| "We'll be working on ADR-NNNN today" | Cache ADR-NNNN proactively after the first RAG read |
| "Let's debug BC X" | Cache BC X's primary ADRs + roadmap slice |
| You're about to call `query_docs` for the second time on similar keywords | Cache the result of that second call |
| `@planner` → `@implementer` handoff involves the same docs | Cache in planner, both agents read from cache |
| One-off "what does ADR-X say about Y" | **Skip caching** — direct RAG is cheaper |

## Anti-patterns

| Wrong | Right |
|---|---|
| `memory.create(filename="/memories/session/ADR-0028.md", content=...)` to "cache the RAG output" | `ctx_index(content=..., source="rag-cache-adr0028-...")` |
| `semantic_search("ADR-0028 RagSession")` instead of RAG | `get_history(id="0028")` or `query_docs(...)` |
| `memory.view` + manual reading to recall a cached doc | `ctx_search(queries=[...], source="rag-cache-...")` |
| `ctx_index` without the `rag-cache-` prefix | Always prefix RAG-derived caches with `rag-cache-` |
| Calling `query_docs` 3+ times for the same ADR in one session | Cache once, recall via `ctx_search` thereafter |
| Caching a one-shot lookup that will never be re-read | Direct RAG — caching is overhead for single use |

## Verification

After step 2:

```
ctx_search(queries=["<any keyword from the cached content>"], source="rag-cache-<your-scope>")
```

Expect: ranked sections with breadcrumbs preserved. If empty, re-check the `source` label spelling and that the markdown was non-empty.

After step 3 across the session, run `ctx_stats` — saved tokens / saving % should grow with each recall.

## Recall query tips (multilingual caveat)

`ctx_search` uses FTS5 BM25 with English Porter stemmer + trigram fallback + RRF. It is **language-agnostic at the token level but has no built-in cross-language bridge** — a Polish query will not auto-match English content the way RAG embeddings + multilingual glossary do.

Empirical (Test 4, 2026-05-27): cached ADR-0016 with English content; query *"jakie są domyślne i maksymalne limity kuponów na zamówienie"* returned zero hits even though the content was present. Query *"where are CouponsOptions configured"* returned top-1 correctly because it contained the literal CamelCase identifier `CouponsOptions`.

**Rules for recall queries against `rag-cache-*` sources**:

1. **Always include at least one code identifier** that appears verbatim in the cached content (CamelCase class/option name, method name, ADR id, KI-NNN code).
2. **Prefer English phrasing** for descriptive parts of the query, matching the language of the cached docs.
3. If you must query in Polish, pair it with the English term or code identifier in the same query (e.g. `"limity kuponów MaxCouponsPerOrder"`).
4. On zero-hit, retry with reworded keywords focused on identifiers — do not assume the cache is broken.

## Refresh / invalidate

- **Overwrite**: re-run `ctx_index` with the **same** `source` label. Replaces the prior content.
- **Drop a single cache**: `ctx_purge(confirm=true, scope="source:rag-cache-<scope>")`.
- **Drop all RAG caches this session**: `ctx_purge(confirm=true, scope="source:rag-cache")` (partial match).

---

## Delegating to a subagent (caller-coordination pattern)

**Verified limitation (2026-05-27, two empirical tests)**: built-in VS Code subagents (e.g. `Explore`) have a **hard tool-surface restriction**. They do NOT expose RAG MCP tools, do NOT expose `ctx_*` tools, and do NOT even expose `tool_search` to load them on demand. Reading the skill correctly identifies the right tools but they cannot be invoked from the subagent context. See [`docs/roadmap/context-mode-integration.md` — LIMIT-1](../../../docs/roadmap/context-mode-integration.md).

**This means the `ctx_index` → subagent-`ctx_search` pattern does not work.** Subagent cannot recall from the FTS5 cache.

**Working workarounds** (pick based on subagent's task):

| Subagent task | Workaround |
|---|---|
| Subagent needs RAG facts to do its work | Parent fetches via `query_docs`/`get_history`, formats relevant chunks as markdown, and **passes them inline in the subagent prompt** (no caching for the subagent's read — it can only use what's in its prompt). |
| Subagent does pure exploration that doesn't need RAG knowledge | Delegate normally — subagent uses its native `read_file`/`grep_search`/`semantic_search` surface. The handoff doesn't apply. |
| Subagent must repeat the same RAG lookup multiple times within its own turn | Not currently solvable from inside the subagent. Either split the task into multiple parent-side delegations (each with the relevant chunks inlined) or use a custom agent (Option C in LIMIT-1) with explicit MCP availability. |

The parent still benefits from `ctx_index` for ITS OWN subsequent recalls — just don't expect the subagent to read from that cache.

## References

- Canonical rules: [`mcp-routing.instructions.md` — RAG ↔ context-mode handoff section](../../instructions/mcp-routing.instructions.md)
- L2 wrapper tool plan: [`docs/roadmap/context-mode-integration.md` Phase 7](../../../docs/roadmap/context-mode-integration.md)
- Pattern background: [`docs/patterns/context-mode-read-write-split.md`](../../../docs/patterns/context-mode-read-write-split.md)
- ADR-0029 (context-mode sandbox): [`docs/adr/0029/0029-context-mode-mcp-sandbox.md`](../../../docs/adr/0029/0029-context-mode-mcp-sandbox.md)
- Empirical POC + 4-test validation: [`.github/context/agent-decisions.md`](../../context/agent-decisions.md) — entry "Copilot / RAG ↔ context-mode handoff (POC + 3 validation tests)"
