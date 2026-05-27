---
applyTo: "**"
---

# MCP routing — single source of truth

> **This file owns** the rules for which MCP server / which tool to call for any user intent. Every other instruction, agent, prompt, and skill file links here instead of restating the rules. If you find a duplicated rule elsewhere, fix it by replacing with a link to this file.

The repo ships **two MCP servers**, both live:

| MCP | Status | Servers | Job |
|---|---|---|---|
| **RAG** | ✅ Live | `ecommerceapp-rag-python`, `ecommerceapp-rag-dotnet` (VS Code); `ecommerceapp-rag` (GitHub.com Copilot, see [.github/copilot/mcp.json](../copilot/mcp.json)) | Answer **knowledge** questions from the indexed `docs/` + `.github/context/` corpus |
| **context-mode** | ✅ Live | `ecommerceapp-context-mode` (VS Code stdio via `docker exec`, see [.vscode/mcp.json](../../.vscode/mcp.json)) | **Execute**, **summarise**, **fetch external URLs**, and **persist FTS5-indexed session memory** through a sandboxed Node runtime + AdGuard DNS allowlist. Wraps [ADR-0029](../../docs/adr/0029/0029-context-mode-mcp-sandbox.md). |

> Both servers must be running for the precedence rules below to apply. If a server is down, see the [Fallback ladder](#fallback-ladder-when-an-mcp-returns-empty).

---

## The whole picture on one screen

```text
  +-----------------------------------------------------------------------------+
  |                              USER PROMPT                                    |
  +------------------------------------+----------------------------------------+
                                       |
                                       v
                         INTENT  CLASSIFICATION
              +------------------------+------------------------+
              |                        |                        |
              v                        v                        v
     "how does X work?"        "run / save / index"     pure code edit
     "what does ADR-N say?"    "summarise file"         (no knowledge,
     "is BC blocked?"          "fetch this URL"          no fetch)
     "any known issue?"        "store this for later"
              |                        |                        |
              v                        v                        v
  +----------------------+  +----------------------+  +----------------------+
  |  MCP #1 -- RAG       |  |  MCP #2 -- CONTEXT-  |  |   no MCP needed      |
  |                      |  |  MODE                |  |   (direct tools)     |
  |  KNOWLEDGE           |  |                      |  |                      |
  |                      |  |  EXECUTION /         |  |  read_file           |
  |  list_adrs           |  |  REDUCTION /         |  |  grep_search         |
  |  query_docs          |  |  EXTERNAL FETCH      |  |  semantic_search     |
  |  read_docs           |  |                      |  |  edit / write        |
  |  get_history         |  |  ctx_stats           |  |                      |
  |                      |  |  ctx_execute         |  |                      |
  |  Source of truth     |  |  ctx_execute_file    |  |                      |
  |  for ANYTHING        |  |  ctx_fetch_and_index |  |                      |
  |  written down        |  |  ctx_insight         |  |                      |
  |  in this repo        |  |                      |  |                      |
  +-----------+----------+  +-----------+----------+  +-----------+----------+
              |                         |                         |
              |  empty / low score?     |  hooks fired:           |
              |  -> tell user index     |   PreToolUse rewrites   |
              |     looks stale         |   PostToolUse compresses|
              |  -> fall back to        |   summary saved by      |
              |     direct read_file    |   PreCompact            |
              |  -> NEVER guess         |                         |
              |     from training data  |                         |
              v                         v                         v
  +-----------------------------------------------------------------------------+
  |     ANSWER  +  precise file links (path#Lstart-Lend)  +  citations          |
  +-----------------------------------------------------------------------------+
```

---

## RAG tools (live)

| Tool | When to use |
|---|---|
| `list_adrs()` | Orientation — "what ADRs exist?", "is there an ADR about X?". Cheap. |
| `query_docs(question, bc?, top_k?)` | Discovery — ranked chunks with file paths + line ranges + scores. Use as a pointer. |
| `read_docs(question, bc?, top_files?)` | **Preferred for reasoning.** Full content of the top-ranked unique files. Use to quote rules, check conformance, understand rationale. |
| `get_history(id)` | Evolution of a specific ADR — all chunks sorted chronologically. |

### Trigger-phrase routing

| Trigger phrase / intent | Tool |
|---|---|
| "list ADRs", "what ADRs exist" | `list_adrs` |
| "ADR-NNNN", "decision on X" | `get_history(id="NNNN")` |
| "how does X work?", general architecture | `query_docs(query="...")` |
| "full content of file X", "all details" | `read_docs(query="...")` |
| Known issues, KI-NNN, "is there a known issue about X" | `query_docs("<topic>")` — **NOT `grep_search`** |
| Project state, blocked BCs, "is BC X ready" | `query_docs("<bc> status")` |
| Agent decisions, prior corrections, "have we decided X before" | `query_docs("<topic>")` |
| Bounded-context map, cross-BC dependencies | `query_docs("<bc> dependencies", bc="<BCName>")` |
| Roadmap status, "what's next for BC X" | `query_docs("<bc> roadmap", bc="<BCName>")` |

> **What `bc=` actually does:** it is a case-insensitive **substring filter on the chunk's `breadcrumb` or `doc_title`** (post-search, see [tools/rag/query.py](../../tools/rag/query.py#L194)). Use it for BC **names** that appear in headings or titles (e.g. `bc="Catalog"`, `bc="Orders"`, `bc="Sales/Orders"`). **Do NOT use `bc="context"`** to target `.github/context/*.md` — those files don't have the word "context" in their headings, so the filter excludes every chunk and returns empty. For knowledge in `.github/context/*.md`, omit `bc=` entirely.

**Forbidden paths for `grep_search` / `read_file` as a first move** — these MUST go through RAG first:

- `.github/context/*.md` (known-issues, project-state, agent-decisions, anti-patterns, repo-index)
- `docs/adr/**`
- `docs/roadmap/**`
- `docs/architecture/bounded-context-map.md`

`grep_search`/`read_file` on these paths is a **fallback only** after `query_docs`/`read_docs`/`get_history` returns empty or low-score. Violating this is a [BLOCKS MERGE anti-pattern](../context/anti-patterns-critical.context.md).

### Recommended flow

```
list_adrs()       → orientation: what exists?
query_docs(q)     → discovery: which files score highest?
read_docs(q)      → depth: reason from the complete document
get_history(id)   → evolution: full amendment chain
```

---

## context-mode tools (live)

| Tool | When to use |
|---|---|
| `ctx_stats()` | "how much context have we saved this session?" / health check |
| `ctx_doctor()` | Server-side diagnostics — run first when other `ctx_*` calls fail. |
| `ctx_execute(lang, code)` | Sandboxed code execution. **Verified langs in the shipped runtime: `js`, `ts`, `sh`, `ruby`, `go`, `rust`, `php`, `perl`, `R`, `elixir`, `csharp`.** Python is NOT shipped (was previously advertised by mistake — never decided in ADR-0029). Use for math, regex, parsing, repo-wide derivations — output only what you `console.log`. |
| `ctx_execute_file(path, lang, code)` | Read a file into the sandbox as `FILE_CONTENT` and derive an answer in code — raw bytes never enter context. Use for files >500 lines or any structural summary. **Path quirk:** sandbox cwd is NOT the repo root. Use absolute paths rooted at the workspace mount (default `/workspace`, parametric — see below) or your scan returns silent zero results. To discover the mount on the current container: `ctx_execute("sh", "echo $CONTEXT_MODE_WORKSPACE")` (one-shot per session; cache the result). |
| `ctx_batch_execute(commands, queries)` | 3+ related commands in one round trip; auto-indexes outputs, returns matching sections per `queries`. Set `concurrency` 2–8 for I/O-bound work. |
| `ctx_index(content\|path, source)` | Persist content into the FTS5 knowledge base (markdown-aware chunking, code blocks intact). Use for docs/skills/API refs you'll need to recall precisely. |
| `ctx_search(queries, source?, sort?)` | Search the FTS5 knowledge base + auto-captured session memory (decisions, errors, blockers, plans). Multi-query batched, Porter+trigram+RRF ranking. |
| `ctx_fetch_and_index(url\|requests, source)` | Pull external URL(s) through AdGuard DNS allowlist, persist as searchable markdown. **Only path** for project-related external URLs. |
| `ctx_insight(port?)` | Open local web UI dashboard (default :4747) for personal analytics across sessions. |
| `ctx_purge(confirm, sessionId?\|scope?)` | **Destructive** — wipe one session or the whole project knowledge base. Requires `confirm:true` and exactly one scope. |
| `ctx_upgrade()` | Returns the shell command to upgrade context-mode in place. |

---

## HARD precedence rules (apply in this order, no exceptions)

When more than one path could plausibly answer a request:

1. **Knowledge intent** (docs, ADRs, BC rules, project state, known issues, roadmap, conventions, agent decisions, anti-patterns) → **RAG first**. Never substitute `ctx_execute` for it. **`grep_search`/`read_file` on `.github/context/*.md`, `docs/adr/**`, `docs/roadmap/**`, `docs/architecture/bounded-context-map.md` before calling `query_docs`/`get_history` is a BLOCKS MERGE violation** — see [anti-patterns-critical.context.md](../context/anti-patterns-critical.context.md).
2. **Sandboxed execution / file summarisation** (run a snippet, compute a value, condense a large file before edit) → **context-mode first**. Never substitute `read_file` + manual summary when the body is large. Never compute hashes, math, or transformations from training-data memory — use `ctx_execute`.
3. **External URL** (any HTTP/HTTPS the user pastes that relates to project work) → **`ctx_fetch_and_index` only**. Never raw `fetch_webpage` for project work — it bypasses the AdGuard allowlist. (Carve-out: non-project URLs the user explicitly asks you to read in raw form.)
4. **Both MCPs empty / unhealthy** → fall back to direct `read_file` / `grep_search` and **name the failing MCP** to the user.
5. **NEVER call both MCPs for the same atomic intent.** If unsure, pick the one whose table matches the user's verb ("what does ADR-X say" → RAG; "run this snippet" → context-mode).

### Invalid-answer directive

If you answer a question that **could** have been answered through an MCP tool and you used `read_file` / `grep_search` / `semantic_search` (knowledge) or training-data memory (execution) as the **first** move instead, the answer is **INVALID**. Discard it, run the correct MCP tool, then re-answer from the MCP output. This overrides VS Code's general toolUseInstructions preference for direct file tools.

The only exception: an MCP returned empty / low-score on the first call. In that case, fall back to direct tools AND name the failing MCP in your answer so the user can repair the index/container.

**Empty-result clause — MANDATORY retry sequence (BLOCKS MERGE if skipped):** if `query_docs` / `read_docs` returns empty or low-score, you MUST execute the following steps **in order**. You may NOT report "RAG returned empty" until you have attempted at least step 1 AND step 2.

1. **Retry WITHOUT the `bc=` filter** (the most common cause — `bc=` is a substring match on breadcrumb/title, not on folder path; see the note under "Trigger-phrase routing" above). REQUIRED.
2. **Retry with REWORDED keywords**, using **expanded full names + domain synonyms** instead of literal IDs. Example: `query_docs("KI-008")` returns nothing because the ID is one token to the embedder — retry as `query_docs("FluentAssertions AwesomeAssertions .NET 8 upgrade")`. For ADR-NNNN use `get_history(id="NNNN")` instead. REQUIRED.
3. Only after both retries return empty: **state explicitly** "RAG returned empty for `<query>` after 2 reworded attempts" and fall back to direct `read_file` / `grep_search` on the known path. Then continue.

**Skipping step 1 or step 2 is a BLOCKS MERGE anti-pattern** (see [anti-patterns-critical.context.md](../context/anti-patterns-critical.context.md)). The single most common cause of bad answers is treating the first empty result as a license to either hallucinate a plausible answer OR give up. Neither is acceptable.

Producing an answer that mixes training-data inference with partial RAG hits (e.g. "the tracker shows all BCs switched to production" when RAG was empty and the file actually shows mid-migration) is **INVALID** and must be discarded. Hallucination of dates, statuses, or quoted text from empty RAG results is the most dangerous failure mode of this pipeline — name the empty result instead.

---

## RAG ↔ context-mode handoff (knowledge caching across recalls)

> **Use case**: same RAG knowledge will be re-read 3+ times in the session (long debug, plan + implement + verify on the same ADR, multi-step refactor referencing the same BC rules). Manually re-calling `query_docs` / `read_docs` re-bills the same tokens every time. Cache the result in context-mode once, recall via `ctx_search` thereafter.
>
> **When NOT to use it**: a one-shot question (answered once, never reread) — direct RAG is cheaper. The handoff costs one extra `ctx_index` call up front; it only pays back after the **second** recall.
>
> **L1 status (this section)**: manual 3-step handoff. **L2 status (future)**: a single `query_docs_cached` wrapper tool — see [docs/roadmap/context-mode-integration.md](../../docs/roadmap/context-mode-integration.md) Phase 7.

### Three similar "memory" surfaces — DO NOT MIX

Three near-identically-named systems exist in this workspace. The L1 handoff uses **only** the third one. Empirical POC (2026-05-27) showed a weaker model picked the wrong one in all 3 steps without this explicit table — see [agent-decisions log](../context/agent-decisions.md).

| Surface | What it actually is | Use for handoff? |
|---|---|---|
| `semantic_search` | **VS Code's embedded code/text search** over the open workspace. Not our RAG. Not chunk-ranked. | ❌ NO. Never substitute for `query_docs`. |
| `memory.create` / `memory.view` (paths `/memories/*`) | **VS Code's persistent notes** for cross-session preferences. Single-doc, no FTS. | ❌ NO. Not searchable for the handoff. |
| `ctx_index` / `ctx_search` | **context-mode's FTS5 knowledge base** with Porter+trigram+RRF ranking. Markdown-aware chunking. | ✅ YES. The only correct cache for RAG handoff. |

### Manual handoff (L1) — three steps

```
  Step 1: RAG (one-time fetch)
  ─────────────────────────────────────
    query_docs(query="...", bc?, top_k=5)
        ↓
    read_docs(query="...")     ← if reasoning needs full file bodies
        ↓
    Format the relevant chunks into a single markdown doc:
      - one H2 per chunk / file
      - preserve breadcrumbs (path + line range)
      - keep code blocks intact (```csharp ... ```)
      - keep tables intact

  Step 2: Cache (one ctx_index call)
  ─────────────────────────────────────
    ctx_index(
      content="<the markdown from step 1>",
      source="rag-cache-<scope>-<topic>"
    )

  Step 3: Recall (any number of times, zero RAG re-bill)
  ─────────────────────────────────────
    ctx_search(
      queries=["specific question"],
      source="rag-cache-<scope>"   ← partial-match works; one prefix covers multiple caches
    )
```

### Naming convention for `source`

The `source` label must be deterministic so subsequent `ctx_search` calls can target it with a partial-match prefix.

| Scope | Pattern | Example |
|---|---|---|
| Specific ADR | `rag-cache-adr<NNNN>-<topic>` | `rag-cache-adr0028-ragsession-icontentsource` |
| Bounded context | `rag-cache-<bc>-<topic>` | `rag-cache-orders-checkout-rules` |
| Cross-cutting area | `rag-cache-<area>-<topic>` | `rag-cache-validation-fluentvalidation-conventions` |
| Roadmap slice | `rag-cache-roadmap-<bc>` | `rag-cache-roadmap-iam-atomic-switch` |
| Known issue | `rag-cache-ki<NNN>` | `rag-cache-ki008` |

Always lowercase, kebab-case, ASCII only. The `rag-cache-` prefix is **mandatory** — it lets you recall any cached RAG content with `source="rag-cache"` (partial match).

### Markdown template for cached content

Use this exact shape so `ctx_search` returns clean ranked sections:

```markdown
# <Topic title>

> Cached from RAG on <date>. Source: query_docs("<original query>"[, bc="<BC>"]).
> Refresh: re-run query_docs and call ctx_index with the same source label to overwrite.

## <First file or chunk title>

**Path**: `relative/path.md#Lstart-Lend`
**Breadcrumb**: <breadcrumb from RAG result>

<chunk body — keep code blocks, tables, lists intact>

## <Second file or chunk title>
...
```

### Trigger heuristics — when to invoke the handoff

| Signal | Action |
|---|---|
| User says "we'll be working on ADR-NNNN today" / "let's debug BC X" | Cache the ADR / BC docs proactively after the first RAG call |
| You're about to call `query_docs` for the **second time** with similar keywords | Cache the result of the second call |
| Plan handoff (`@planner` → `@implementer`) involves the same docs | Cache once in planner output, both agents read from cache |
| One-off question, low chance of re-reading | **Skip** — direct RAG is cheaper |

### Anti-patterns (BLOCKS MERGE if repeated after this rule)

| Wrong | Right |
|---|---|
| `memory.create(filename="/memories/session/ADR-0028.md", content=...)` to "cache RAG output" | `ctx_index(content=..., source="rag-cache-adr0028-...")` |
| `semantic_search(query="ADR-0028 RagSession")` instead of RAG | `query_docs(query="...")` or `get_history(id="0028")` |
| `memory.view` + manual reading to recall the cached doc | `ctx_search(queries=[...], source="rag-cache-...")` |
| `ctx_index` without the `rag-cache-` prefix | Always prefix RAG-derived caches with `rag-cache-` |
| Calling `query_docs` 3+ times for the same ADR in one session | Cache once, recall via `ctx_search` thereafter |

For a step-by-step walkthrough with examples, see the skill [`.github/skills/rag-with-memory/SKILL.md`](../skills/rag-with-memory/SKILL.md).

---

## Fallback ladder (when an MCP returns empty)

1. **RAG empty** → say "no chunks found, index may be stale, suggest `python tools/rag/ingest.py`" → only then `read_file` / `grep_search` for known paths.
2. **context-mode empty / unhealthy** → say which hook/tool failed → fall back to direct tools, document the failure in the answer.
3. **Both empty** → answer from direct workspace tools only; flag the routing failure explicitly so the user can fix the index/container.

**Never guess from training data** for a question that has a definitive answer in the project docs.

---

## RAG maintenance — re-index requirements

| Change | Re-index needed? |
|---|---|
| `multilingual-glossary.yaml` edited | ❌ Query-time only |
| `rag-config.yaml` ranking weights changed | ❌ Query-time only |
| `queries.yaml` edited | ❌ Not used at ingest |
| Any `docs/` or `.github/context/` file changed | ✅ Incremental (`python tools/rag/ingest.py`) |
| `metadata-rules.yaml` changed | ✅ Force-full (`ingest.py --force-full`) |
| `embedder.model` or `chunker.*` changed | ✅ Force-full |

## RAG maintenance — which skill to load

| Symptom | Skill |
|---|---|
| MCP not starting, errors, all scores < 0.25, DLL lock | `.github/skills/diagnose-rag/` |
| Correct English query works but PL/DE returns wrong doc | `.github/skills/expand-rag-glossary/` |
| Right doc consistently at #3–5 instead of #1 | `.github/skills/tune-rag-weights/` |
| New `docs/` folder added or wrong `doc_kind` on a file | `.github/skills/generate-rag-rules/` |
| A file has no named eval query covering it | `.github/skills/generate-eval-questions/` |
| Full maintenance cycle | `/rag-sync` prompt |

---

## Further reading

- Full architecture: [docs/rag/rag-architecture.md](../../docs/rag/rag-architecture.md)
- Error envelope spec: [docs/rag/rag-architecture.md §14](../../docs/rag/rag-architecture.md#14-error-handling-sanitisation-and-middleware)
- Migration playbook (anti-patterns + verification prompts): [docs/rag/mcp-first-routing-migration-playbook.md](../../docs/rag/mcp-first-routing-migration-playbook.md)
- ASCII flow + multi-MCP coexistence detail: [playbook §13](../../docs/rag/mcp-first-routing-migration-playbook.md#13-coexistence-with-a-second-mcp-server-worked-example-context-mode)
- ADR-0027 (RAG), ADR-0028 (remote multitenant), ADR-0029 (context-mode sandbox)
