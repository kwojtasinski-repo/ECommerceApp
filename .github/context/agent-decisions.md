# Agent Decisions Log

> **Append-only log of in-session corrections to AI agent behavior.**
> Operational, not architectural. For _why the system is built that way_ → ADRs in `docs/adr/`.
> For _how the agent should behave / what it missed / what to never repeat_ → here.
>
> **Read before non-trivial agent work** to avoid repeating past mistakes.
> **Append after any meaningful correction** that an agent received during a session.

---

## When to write here vs. an ADR

| Situation                                                                   | Goes to              |
| --------------------------------------------------------------------------- | -------------------- |
| Architectural decision (BC, pattern, technology, cross-cutting)             | ADR                  |
| "Will I want to explain this to a new dev in a year?"                       | ADR                  |
| Agent missed a guard, forgot to read a context file, ignored an instruction | `agent-decisions.md` |
| Naming/format nit that the agent kept getting wrong                         | `agent-decisions.md` |
| Tool selection mistake (used wrong skill, wrong scope)                      | `agent-decisions.md` |
| Recurring drift you keep correcting in chat                                 | `agent-decisions.md` |

**Promotion rule**: if the same correction appears **2+ times** → promote to a permanent rule:

- Architectural rule → `anti-patterns-critical.context.md` or relevant `*.instructions.md`
- Workflow rule → relevant agent file (`bc-switch.md`, `code-reviewer.md`, etc.)
- Decision-level rule → new ADR via `@adr-generator`

When promoted, mark the entry **Status: Promoted → ADR-NNNN** (or file ref) and keep it for history.

---

## Entry format (Variant A — required)

```markdown
## YYYY-MM-DD — <agent-name> / <area>

- **Context**: What the agent tried to do.
- **Decision**: What the human decided instead (NO / YES / different approach).
- **Rationale**: Why — link to project-state line, ADR, instruction file, or commit.
- **Action**: What changes to instructions/agents/skills should follow (one concrete action).
- **Promote?**: When does this graduate to a permanent rule (e.g. "after 2nd occurrence → anti-patterns-critical").
- **Status**: Open | Resolved | Promoted → <ref>
```

Rules:

- One H2 per entry. **Append, do not edit history**.
- Date in `YYYY-MM-DD` format (today's real date).
- Keep entries scannable — 5–10 lines each. Link, don't quote.

---

## 2026-05-28 — Agent / RAG auto-cache hook (L3) pattern

- **Trigger / Mistake**: The L2 path (`query_docs_cached` + manual `ctx_index`) requires the agent to remember a follow-up call; agents skipped it ~1 in 4 turns, degrading the RAG-with-memory pattern back to plain RAG.
- **Correction**: Shipped a host-side PostToolUse hook (`.github/hooks/auto-cache.mjs`) that auto-indexes every RAG tool response into context-mode's FTS5 store under the `rag-auto-` source prefix. Tool detection uses **runtime introspection** of `.vscode/mcp.json` (cached 1h, lock-coalesced) so newly-added RAG tools are auto-discovered without code changes.
- **Rationale**: [ADR-0029 Amendment 1](../../docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md). Removes the most-skipped step in the RAG-with-memory pattern. Per-fire latency ~290–310ms, off the agent's turn wall-clock.
- **Action**: Updated [mcp-routing.instructions.md](../instructions/mcp-routing.instructions.md) — agent no longer needs an explicit `ctx_index` after RAG calls. Manual L1/L2 paths still work and interoperate (`ctx_search(source="rag-")` recalls both `rag-cache-` and `rag-auto-`).
- **Promote?**: Already promoted (ADR + instruction). This entry serves as the audit trail.
- **Status**: Resolved → ADR-0029 Amendment 1 + [docs/rag/auto-cache-hook.md](../../docs/rag/auto-cache-hook.md).

## 2026-05-28 — Agent / Shape-driven formatter beats tool-name dispatch

- **Trigger / Mistake**: First version of `auto-cache.mjs` dispatched by tool name (`query_docs` vs `read_docs` vs `get_history`) and emitted only headers for `read_docs` because that response shape has `files[].chunks[].text` (nested), not `files[].text` (flat). Cache entries were 163B — useless on recall.
- **Correction**: Rewrote the formatter to traverse the response shape: detect `files[]` / `hits[]` / `results[]`, then for each item walk **both** `text|content|snippet` at the top level AND nested `chunks[]`. Cache size for the same `read_docs` query jumped to 994B with full chunk text.
- **Rationale**: RAG response shapes differ between tools, between transports (stdio/HTTP), and across server implementations (.NET/Python). A shape-driven walker is the only formatter that survives upstream schema drift.
- **Action**: When extending the formatter for new tool shapes, add a **shape detector** (presence of a key), not a tool-name branch. The fallback path (`renderItem`) must always look for nested chunks before giving up.
- **Promote?**: After 2nd occurrence of "tool-name dispatch missed a nested shape" → promote to an anti-pattern entry in `anti-patterns-critical.context.md`.
- **Status**: Open — single occurrence; rule documented in [docs/rag/auto-cache-hook.md](../../docs/rag/auto-cache-hook.md) §"Source labels".

## 2026-05-28 — Agent / JSONC parser must be string-aware

- **Trigger / Mistake**: First `readJsonc` in `auto-cache.mjs` used `raw.replace(/\/\/.*$/gm, "")` to strip line comments. `.vscode/mcp.json` contains `"http://localhost:6333"` — the regex ate everything after `//` inside the string, producing invalid JSON at position 690 → introspection silently failed and the fallback list was used every time.
- **Correction**: Replaced the regex with a single-pass state machine (`stripJsonc`) that tracks `inString` / `stringQuote` and only strips comments outside strings.
- **Rationale**: Any time we parse a config file that may contain URLs, escape sequences, or `/* */` inside strings, naive regex stripping is a foot-gun.
- **Action**: When writing JSONC helpers, default to a string-aware stripper. The regex-only form is acceptable ONLY when content is guaranteed not to contain `//` or `/*` inside strings.
- **Promote?**: After 2nd occurrence (e.g. another JSONC consumer in repo with the same bug) → promote to a small reusable helper in `tools/` and an anti-pattern entry.
- **Status**: Open — single occurrence so far.

---

## 2026-04-27 — Copilot / RAG MCP server config location

- **Context**: Agent created `.github/copilot/mcp.json` to register the RAG MCP server, then told the user the server was registered. VS Code's MCP browser showed no servers.
- **Decision**: The correct location is `.vscode/mcp.json`. `.github/copilot/mcp.json` is not read by VS Code's MCP server browser — it is only relevant for GitHub Codespaces / future GitHub Copilot tooling.
- **Rationale**: VS Code reads workspace MCP config from `.vscode/mcp.json`. The `.github/copilot/` path has no VS Code runtime effect.
- **Action**: Always create `.vscode/mcp.json` for VS Code MCP registration. Keep `.github/copilot/mcp.json` as a secondary copy for Codespaces compatibility only.
- **Promote?**: After 2nd occurrence → add to `docs-index.instructions.md` or a tooling note.
- **Status**: Resolved
- All entries in **English** for AI parsability.

---

## Example entry (template — replace with real ones)

## 2026-04-21 — bc-switch / Sales/Payments

- **Context**: Agent attempted to delete `Application/Services/Payments/PaymentHandler.cs` as part of the atomic switch.
- **Decision**: Do NOT delete during the switch.
- **Rationale**: `project-state.md` notes "Legacy `PaymentHandler` retained for Step 5 cleanup". The agent skipped reading project-state and assumed atomic switch implies delete-all-legacy.
- **Action**: Strengthen `bc-switch.md` Step 1 to require quoting the project-state line for the BC before any delete operation.
- **Promote?**: After 2nd occurrence → add explicit anti-pattern to `anti-patterns-critical.context.md` ("No legacy delete without project-state quote").
- **Status**: Open

---

## Entries

<!-- Append new entries below this line, newest at the bottom. -->

## 2026-05-18 — Implementer / RAG .NET configuration discovery

- **Context**: While stabilising `tools/rag-dotnet` for local dev, the plan referenced a non-existent `tools/rag-dotnet/rag-config.yaml` and a Python-venv `optimum-cli` step. Both wrong.
- **Decision**: (1) The .NET path **shares** `tools/rag/rag-config.yaml` with Python — Dockerfile literally does `COPY ../rag/rag-config.yaml /app/rag-config.yaml`. No separate .NET config exists. (2) The HuggingFace ONNX bundle (`/onnx/model.onnx` + `vocab.txt` + `tokenizer.json` + `config.json`) is pre-exported by sentence-transformers maintainers, so a PowerShell/curl download replaces the Python optimum-cli stage entirely.
- **Rationale**: Source of truth verified in `tools/rag-dotnet/Dockerfile` line ~45 and HuggingFace repo for `paraphrase-multilingual-MiniLM-L12-v2`.
- **Action**: `RagConfig.ResolveConfigPath` uses 4-way priority: explicit arg → `RAG_CONFIG` → `RAG_WORKSPACE`-derived `<ws>/tools/rag/rag-config.yaml` → `AppContext.BaseDirectory/rag-config.yaml`. `RagConfig.Workspace` derives from config-path grandparent (Python parity with `config_path.parents[2]`), then `RAG_WORKSPACE`, then cwd. Local devs run `pwsh tools/rag-dotnet/download-model.ps1` once; Docker uses `curlimages/curl` stage. **Never invent `tools/rag-dotnet/rag-config.yaml` again.**
- **Status**: Resolved

---

## 2026-05-19 — Implementer / RAG tool and test self-containment

- **Context**: When implementing .NET e2e tests the agent initially considered referencing real EcommerceApp ADR numbers and domain entities (e.g. `CustomerId`, `ADR-0016`) in the synthetic test workspace.
- **Decision**: Both RAG implementations (Python `tools/rag/` and .NET `tools/rag-dotnet/`) must be **self-contained**. Tests must use a synthetic workspace with domain-neutral content (no EcommerceApp ADR numbers, entity names, or bounded-context identifiers). The workspace is created in a temp directory with a UUID-suffixed collection name. See ADR-0027 §9 for the full rule.
- **Rationale**: Self-containment lets the RAG tooling be reused in other projects without modification and lets CI run e2e tests without a full repo checkout.
- **Action**: When writing or reviewing RAG tests: reject any fixture that imports real docs, real ADR paths, or real entity names from EcommerceApp. `SyntheticWorkspace.cs` and `conftest.py` are the approved patterns.
- **Promote?**: After 2nd drift → add to `dotnet.instructions.md` under RAG test rules.
- **Status**: Resolved

---

## 2026-05-19 — Implementer / RAG multilingual glossary expansion

- **Context**: After the multilingual glossary was added (one-shot append), Polish and German "known issues FluentAssertions" queries still returned the wrong document at #1. Mean pooling gives equal weight to every token — a 7-word PL/DE query with a 10-word English expansion appended once yields only ~33–37% English weight, insufficient to overcome the semantic pull of generic words like `Fehler`/`b³êdy` toward unrelated error-handling docs.
- **Decision**: Repeat the English expansion **3 times** (not replace the non-English words — too aggressive). `return query + (" " + expansion) * 3` in Python; `Enumerable.Repeat(" " + expansion, 3)` in .NET. This raises English weight to 60–87% for typical short queries.
- **Rationale**: Mean pooling is linear: token count drives weight. Repetition is the cheapest, safest amplification — it degrades gracefully (English-only queries are never touched because only non-ASCII patterns fire) and requires no model change or re-index.
- **Pitfall 1 — `@dataclass` silently dropped**: When inserting `_expand_query` and `QueryHit` as a file-level replacement, the `@dataclass` decorator was inadvertently omitted from `QueryHit`. The class appeared valid (field annotations existed) but calling `QueryHit(rel_path=..., ...)` raised `TypeError: QueryHit() takes no arguments`. Fix: always read the full class definition before replacing; never omit decorators during partial replacements.
- **Pitfall 2 — `_glossary` missing after `__new__`**: `make_engine_with_stubs()` in tests uses `QueryEngine.__new__()` to bypass `__init__`, so the `engine._glossary = []` assignment inside `__init__` was skipped → `AttributeError`. Fix: after any `__init__` addition, check all `__new__`-based test factories and add the new attribute.
- **Pitfall 3 — .NET MCP build lock**: `dotnet build` while VS Code holds the .NET MCP server process running will fail (DLLs locked). **Always warn the user** before running `dotnet build` on any project whose output is an active MCP server; ask them to stop the server first.
- **Action**: §10 added to ADR-0027; repeat=3 documented; glossary gap for `Bezeichner` (DE entity identifier) added to both glossary YAMLs; conformance checklist items added.
- **Status**: Resolved

---

## 2026-05-23 — RAG SSE ingest pipeline stabilisation session

### ZIP validation (upload endpoint)

- **Decision**: Every POST to `/ingest/{collection}/batch` must contain `metadata-rules.yaml` AND `queries.yaml` inside the ZIP. Missing either → 400. Both files are parsed and validated (non-empty `doc_kind_rules`, non-empty `named_queries`, cross-validated `doc_kind` vocabulary). Config files are then filtered out before ingest — they are not indexed as documents.
- **Rationale**: Uploading without config files led to silent doc_kind and adr_id misdetection (fell back to built-in defaults that don't match the repo's ADR folder structure). Validation at upload time fails fast and gives the caller a clear error.
- **Action**: Implemented in `ingest_routes.py` (Python) and `IngestController.cs` (.NET). Both servers validated; Python and .NET unit + E2E test suites updated.
- **Status**: Resolved

### Operation manifest on status endpoint

- **Decision**: `GET /ingest/{collection}/operations/{opId}` returns a `manifest` object **only when status == Completed**. The manifest contains `{ indexedChunks, docKind }`. No new endpoint was created — this is intentional; one endpoint per operation is enough.
- **Rationale**: User said "we don't need to create a new one". Embedding manifest in the existing status response avoids an extra round-trip and keeps the API surface small.
- **Action**: `IngestOperation` (Python) and `IngestOperationResult` (C#) both carry `doc_kind` now. `mark_completed` / `MarkCompleted` accepts `doc_kind`. Workers return `(chunk_count, doc_kind)` tuple. The `chunkCount` top-level field was removed from the Python response (it was redundant with `manifest.indexedChunks`). C# uses a computed `Manifest` property with `JsonIgnore(WhenWritingNull)`.
- **Status**: Resolved

### get_history score-threshold bug

- **Decision/Bug**: `QueryEngine.search()` was applying `score_threshold=0.30` even when `field_filter` was set (i.e. for `get_history` exact-metadata lookups). This caused `get_history('0006')` to return 0 chunks because the query string `"history 0006"` has low cosine similarity to TypedId ADR content, even though the chunks ARE in the index.
- **Fix**: Skip threshold when `field_filter` is provided. The vector score is only used for ranking, not filtering, in exact-metadata mode.
- **Pitfall**: Never apply `score_threshold` unconditionally when the query is an exact metadata filter rather than a semantic search. These two modes have fundamentally different semantics.
- **Status**: Resolved

### Pipeline test cwd bug (phase 2 docker build)

- **Decision/Bug**: `_run_stream` in `test_full_pipeline.py` passed `docker build ... tools/rag/` as a relative path. When pytest ran from `tools/rag/`, the path resolved to `tools/rag/tools/rag/` (non-existent). Fix: added `cwd` parameter to `_run_stream`; phase 2 calls both docker builds with `cwd=str(WORKSPACE)`.
- **Status**: Resolved

### VECTOR_MODE env var precedence

- **Decision**: Python `common.py` resolves `vector_mode` as `os.environ.get("VECTOR_MODE") or self.raw["vector_store"]["mode"]`. The env var takes priority over config file. This was broken (baked `ENV VECTOR_MODE=local` in Dockerfile prevented Docker-mode runtime). Fix: removed the baked env var from Dockerfile; the config file now controls defaults.
- **Action**: `tools/rag/Dockerfile` must NOT bake `ENV VECTOR_MODE=...`. Set it only in `docker-compose.yaml` env sections or at runtime.
- **Status**: Resolved

### Known pre-existing issue (not fixed this session)

- **Issue**: Python STDIO `get_history('0006')` returned 0 chunks in the pipeline test (Phase 3) even after the score-threshold fix was applied to the local venv. Root cause (post-fix suspicion): the Phase 3 STDIO ingest uses a Docker-launched Qdrant; the fix is only in the local Python source, not yet rebuilt into the Docker image. After Docker rebuild this should resolve.
- **Status**: Open — will resolve after next `docker compose build rag-tools`

---

## 2026-05-26 — Planner / context-mode MCP sandbox design (ADR-0029 DRAFT)

- **Context**: Multi-round design session for context-mode (external MCP for ~98% Copilot context reduction). Started with `--network none` only; pivoted to custom `ctx-net` bridge + AdGuard DNS firewall after recognising `ctx_fetch_and_index` value. Dozzle was initially planned as 3rd container, then dropped for simplicity. Phase 6 (auto-triage suggestions → blacklist with bot commits) was scoped, then deferred to a future amendment because community lists already cover ~95% of cases.
- **Decision**: ADR-0029 DRAFT created at `docs/adr/0029/0029-context-mode-mcp-sandbox.md` with 8-point Decision: (1) build from pinned tag, (2) 6 hardening flags, (3) `ctx-net` bridge with `dns:[adguard]`, (4) AdGuard 4-file config + auto-update community lists every 168h, (5) `network-monitor.js` hook as secondary signal, (6) append-only section 13 in copilot-instructions, (7) hooks via `docker exec`, (8) container logs via VS Code Docker extension (NO dedicated log viewer container). Roadmap files: `docs/roadmap/context-mode-integration.md` (5 phases + future Phase 6) and `docs/roadmap/context-mode-details.md` (full configs including AdGuard YAML, blacklist/whitelist examples, REST API self-scanning sketch, Phase 6 deferred plan).
- **Rationale**: User priorities surfaced through dialogue — simplicity over fancy UI, team-grade safety without bureaucracy, license-clean for 300+ employee internal use. AdGuard chosen over mitmproxy (community lists + UI), over Portainer (no URL filtering), over Falco (requires privileged). Dozzle dropped because VS Code Docker extension covers debugging.
- **Action**: Files committed in DRAFT status — do not reference ADR-0029 from other docs until status flips to Accepted post-implementation kick-off. When implementation starts, **read this entry first** to avoid re-litigating: Dozzle is out, Phase 6 is future amendment, `ctx-net` is custom bridge not `--network none`, all 6 hardening flags are mandatory (conformance checklist enforces).
- **Pitfall flagged for implementation**: Docker Desktop is paid for companies >250 employees OR >$10M revenue. Repo MUST stay runtime-agnostic — all configs use `docker` CLI verb, but per-developer swap to `podman`/`nerdctl` must remain a local-only change (never committed). Verify `ctx-net` bridge works identically on Rancher Desktop (containerd backend) and Podman (rootless) before declaring Phase 1 complete.
- **Pitfall flagged for AdGuard config**: `bind_host: 0.0.0.0` inside the container is required (so other containers on `ctx-net` can reach it), but `ports:` mapping must use `127.0.0.1:3000:3000` to keep the UI off the host's external interface. Never expose AdGuard UI to `0.0.0.0:3000` on the host.
- **Promote?**: This is a one-off design log entry. The decisions live in ADR-0029 and the roadmap files. No promotion target unless we hit the same "scope creep with too many containers" pattern again — then promote a "minimal containers rule" to instructions.
- **Status**: Resolved (DRAFT serialised; implementation pending)

---

## 2026-05-26 — Planner / context-mode auto-triage scope + Azure DevOps constraint

- **Context**: After serialising ADR-0029, user asked who and how would scan traffic + feed entries to AdGuard. Proposed full-auto pipeline (cron → threat intel feeds URLhaus/OpenPhish/VirusTotal → classification → auto-PR → conditional auto-merge → webhook notify) with safety rails (rollback window 24h, 2+ feeds threshold, allowlist override, max 10 PR/day). User declined the upgrade: "wolałbym pełną automatyzację … niestety działa na Azure DevOps więc github actions odpadają :// dobra przeżyje bez tych automatów na razie zaaktualizuj plan".
- **Decision**: Phase 6 (auto-triage + suggestions UI) **stays as a future amendment** — NOT promoted to mandatory Phase 5. Recorded as A17 in `integration.md`: team CI is Azure DevOps, not GitHub; if/when Phase 6 is implemented, the pipeline must use Azure Pipelines + `az repos pr create` (NOT `gh pr create`) and local trigger can be Windows Task Scheduler.
- **Rationale**: Community lists + Google Safe Browsing already cover ~95% of cases. Phase 6 automation is an optimisation, not a blocker. Building Azure DevOps integration before Phase 5 is even deployed = premature.
- **Action**: Replaced `GitHub Action` references in `details.md` and `integration.md` with `Azure DevOps Pipeline` (+ explicit "GitHub Actions niedostępne" note). Added założenie A17 to `integration.md`. **Implementation rule for whoever picks up Phase 6 in the future**: do NOT scaffold `.github/workflows/*.yml` — use `azure-pipelines.yml` or local Task Scheduler script. The current `gh pr create` snippet in `details.md` Phase 6 PowerShell example must be replaced with the `az repos pr create` equivalent before that section becomes real.
- **Pitfall flagged**: ADR-0029 currently has no explicit "CI = Azure DevOps" mention — only the roadmap does. If Phase 6 ever gets a follow-up ADR, that ADR MUST repeat the constraint or someone will scaffold GitHub Actions and waste a day.
- **Promote?**: Resolve at Phase 6 design time — promote A17 to a permanent instruction note (`.github/instructions/ci-platform.instructions.md`) only if we hit a 2nd case of "agent assumed GitHub Actions". Currently a one-off.
- **Status**: Resolved (plan updated; Phase 6 explicitly deferred)

---

## 2026-05-27 — Copilot / context-mode read/write/execute split

- **Context**: After empirical mixed-workload tests on context-mode v1.0.151, the question came up: should the sandbox be used for edits too (writable mount)?
- **Decision**: NO. Keep the three-path split: READ/derive via `ctx_execute(_file)` (sandbox, `/workspace:ro`), WRITE via native VS Code edit tools (`replace_string_in_file` / `create_file` / `multi_replace_string_in_file`), EXECUTE on host (build/test/git) via `run_in_terminal`. Captured in [docs/patterns/context-mode-read-write-split.md](../../docs/patterns/context-mode-read-write-split.md).
- **Rationale**: Reads dominate token cost (full content) — sandboxing them saves ~90% on derivation workloads (verified: 700-line file ~8.5K tok → 300 tok). Writes are diff-sized and already cheap; sandboxing them would break undo/git/permissions for ~0% saving. Execute paths are stateful (your SDK, secrets, cwd) and cannot run in an ephemeral `:ro` sandbox.
- **Action**: New pattern doc (above). Added link from [ADR-0029 References](../../docs/adr/0029/0029-context-mode-mcp-sandbox.md#references). Added cwd-quirk note to [mcp-routing.instructions.md](../instructions/mcp-routing.instructions.md) for `ctx_execute_file` (sandbox cwd != repo root → use absolute `/workspace/...` paths or get silent zero results).
- **Promote?**: After 2nd occurrence of someone proposing `:rw` mount OR using `ctx_execute` for git/build → promote pattern doc rules into `anti-patterns-critical.context.md`.
- **Status**: Resolved (pattern doc + ADR cross-link + routing note in place)


---

## 2026-05-27 — Copilot / RAG ↔ context-mode handoff (POC + 3 validation tests)

- **Context**: After ADR-0029 (context-mode sandbox) and the read/write/execute pattern doc shipped, the next obvious efficiency question was: can context-mode's FTS5 store act as a session-local cache for RAG knowledge, so repeated re-reads of the same ADR/BC docs don't re-bill RAG? Manual POC was run end-to-end (`query_docs` → format markdown → `ctx_index(source="rag-cache-adr0028-...")` → 3× `ctx_search`). The cache returned correctly ranked sections with breadcrumbs and code blocks intact. `ctx_stats` reported 30.7% saving on 2 calls; projected 75–88% on 5–10 recalls.
- **Test 1 — primary agent, this session** (Claude): cached ADR-0027 end-to-end, 9/9 recall queries returned correct sections, `ctx_stats` showed 55% reduction on 2 calls. **PASS.**
- **Test 2 — subagent, no diagnostic probe** (GPT-5-mini via `runSubagent` Explore): subagent read the skill correctly and **identified** the right tools (`get_history`, `ctx_index`, `ctx_search`) but **could not invoke** them — fell back to `memory.create` / `memory.view` reporting "MCP tools not available in this environment". Did NOT use `semantic_search` (skill's three-surface table worked). Used correct naming convention `rag-cache-adr0029-context-mode`. **Partial PASS** for tool selection, **0/3** for execution. Unclear if bootstrap or hard restriction.
- **Test 3 — subagent, with explicit `tool_search` probe** (GPT-5-mini): subagent was instructed to call `tool_search` three times to load MCP tools before doing anything. Result: `tool_search` itself reported unavailable in all 3 attempts. Zero MCP tools loadable. **Hard surface restriction CONFIRMED** — not a bootstrap issue. Built-in `Explore` subagent has a fixed tool set that excludes `tool_search`, RAG MCP, and context-mode MCP entirely.
- **Test 4 — fresh chat window, primary agent** (user-driven, Claude): pasted L1 skill-only prompt into a clean chat. Agent used `get_history(id="0016")` → `ctx_index(source="rag-cache-adr0016-coupons")` → `ctx_search(...)`. `ctx_stats`: 60.8% reduction. **PASS for tool selection.** One recall (Polish-language query without code identifier) returned zero hits — FTS5 multilingual gap, documented as a recall-query caveat in the skill.
- **Decision**: Ship **L1 (documentation-only) as final for parent-agent recalls**. Defer **L2 (single `query_docs_cached` wrapper tool)** to roadmap Phase 7 as parent-side ergonomic improvement, not a subagent fix. For subagent delegations, ship **inline-chunks pattern** (parent fetches RAG, formats markdown, passes content directly in subagent prompt — no MCP calls expected from subagent).
- **What got shipped (L1)**:
  - `.github/instructions/mcp-routing.instructions.md` — new "RAG ↔ context-mode handoff" section with three-surface table, naming convention, markdown template, trigger heuristics, anti-patterns.
  - `.github/skills/rag-with-memory/SKILL.md` — step-by-step walkthrough, Step 0 workspace-mount probe, "Delegating to a subagent" caller-coordination section (corrected after Test 3), multilingual recall caveat.
  - `docs/patterns/context-mode-read-write-split.md` — "Integration with RAG" section + "How to discover the workspace mount path" probe recipe; all `/workspace` hardcodes replaced with `$CONTEXT_MODE_WORKSPACE` placeholder + default note.
  - `docker-compose.yaml` — context-mode service mount target and env var both parametric: `${CONTEXT_MODE_WORKSPACE:-/workspace}`. Forks override via `.env.context-mode` without doc rewrites.
  - `docs/roadmap/context-mode-integration.md` — Phase 7 (L2 wrapper) + "L1 ship status & open follow-ups" section with verified LIMIT-1 (subagent surface restriction), LIMIT-2 (probe enforcement), and corrected Phase 7 acceptance criteria.
- **Rationale**: The subagent restriction is a hard environmental limit we cannot work around with `tools:` allowlist edits (verified: no agent in `.github/agents/*.md` lists MCP tools in its allowlist, yet parent-agent prose calls them successfully — therefore the gate is not the allowlist). L1 docs + inline-chunks pattern is the maximum we can do today. L2 wrapper would still be an MCP tool, so even L2 will not be callable from a built-in subagent — its sole benefit is collapsing the manual 3-step handoff into one call **for the parent agent**.
- **Promote / next?**:
  - L2 (Phase 7) promotion trigger: parent agent in a real session repeatedly does manual 3-step handoff and the friction matters (e.g. forgotten step, wrong source label, etc.). Pure cost savings alone (~5% extra vs manual) does not justify the implementation effort.
  - Subagent MCP delivery (custom agent or vendor change): empirical next step would be creating `.github/agents/explorer-with-mcp.md` to test whether custom agents bypass the surface restriction. Deferred until inline-chunks pattern proves insufficient for actual subagent workloads.
  - Multilingual recall: documented as a caveat ("write recall queries in English or include CamelCase identifiers"); no code change planned. Promote to a separate roadmap item only if it blocks real work.
- **Status**: Resolved. L1 shipped, validated across 4 tests (2 PASS, 1 partial, 1 confirms hard limit). L2 + subagent-fix remain on roadmap with explicit trigger conditions.

## 2026-05-27 � implementer / context-mode Phase 3 hooks

- **Context**: Agent (me) initially dismissed the Phase 3 hooks plan claiming VS Code Copilot Chat has no native hook mechanism, suggesting the .github/hooks/context-mode.json file would be `dead config''. This was wrong on two counts.
- **Decision**: (1) VS Code Copilot Chat DOES have native hook support (Preview) � default location is `.github/hooks/*.json`, all 5 lifecycle events in the plan are real. Verified via `ctx_fetch_and_index(https://code.visualstudio.com/docs/copilot/customization/hooks)`. (2) The roadmap's command string `context-mode hook vscode-copilot <event>` is broken � there is no `context-mode` executable on PATH in the container; `/app/bin/` ships only `statusline.mjs`. The working invocation is `node /app/cli.bundle.mjs hook vscode-copilot <event>`.
- **Rationale**: Verified by `docker exec ecommerceapp-context-mode which context-mode` (empty) and `docker exec ecommerceapp-context-mode node /app/cli.bundle.mjs hook vscode-copilot pretooluse` (exit 0). Without the probe, restart of VS Code would have produced exit 127 silently across every hook fire.
- **Action**: (a) Never dismiss a planned integration based on agent's prior knowledge � verify via official docs through `ctx_fetch_and_index` first. (b) Always probe container CLI commands referenced from external configs (hooks, agents, scripts) with `docker exec` before relying on them at runtime. (c) Roadmap files `context-mode-integration.md �Phase 3` and `context-mode-details.md �Phase 3` should be amended to use `node /app/cli.bundle.mjs hook` instead of the non-existent `context-mode` wrapper.
- **Promote?**: After 2nd occurrence of either failure mode � add to `anti-patterns-critical.context.md` (`Never dismiss external integration plans without official-docs verification`) and to `pre-edit.instructions.md` (`Probe external CLI commands referenced from generated configs before commit`).
- **Status**: Promoted → [pre-edit.instructions.md](../instructions/pre-edit.instructions.md) "Verify external-tool capabilities empirically before documenting them" (covers the CLI-probe half of this entry; the "verify via official docs" half remains as an open guideline for future promote on 2nd occurrence).

## 2026-05-27 — implementer / context-mode runtime list mismatch (2nd occurrence pattern)

- **Context**: While reviewing `ctx_doctor` output as part of Phase 3 hook verification, found that `.github/instructions/mcp-routing.instructions.md` (line 120) advertised "Verified langs in the shipped runtime: `js`, `ts`, `sh`, `ruby`, `go`, `rust`, `php`, `perl`, `R`, `elixir`, `csharp`" — but `ctx_doctor` reports `Runtimes: 2/11 (18%) — javascript, shell`. Empirical check: `ctx_execute(language="csharp", code="...")` returns `Runtime error: C# not available. Install dotnet-script via …`. Same will fail for `ruby`, `go`, `rust`, `php`, `perl`, `R`, `elixir`, `typescript`. The schema enum accepts the strings; the runtime image does not include the interpreters.
- **Decision**: Fixed line 120 to state "**Only `javascript` and `shell` are installed in the shipped runtime image** (`ctx_doctor` → `Runtimes: 2/11`). The schema enum also accepts `typescript`, `ruby`, `go`, `rust`, `php`, `perl`, `R`, `elixir`, `csharp`, `python` — calling any of those returns a runtime error like `C# not available. Install dotnet-script via …`."
- **Rationale**: This is the **2nd occurrence** of the same pattern. The 1st was Python: previously advertised as a supported runtime, corrected inline in mcp-routing.instructions.md to "Python is NOT shipped (was previously advertised by mistake — never decided in ADR-0029)". Today's correction is identical in shape but covers 9 other languages. Pattern = "trusting the schema enum or upstream README as proof of shipped capability instead of empirically probing with `ctx_doctor` or a one-line `ctx_execute` test".
- **Action**: (a) Today's inline fix covers the documentation drift. (b) For prevention, add a one-liner to `.github/instructions/pre-edit.instructions.md` under the existing "Probe external CLI commands" rule: "Before listing a runtime/language/feature of an external tool in our docs, verify it empirically (`ctx_doctor` for context-mode runtimes; a smoke `ctx_execute` call; or the upstream tool's own health output). Schema enums and upstream README claims are not proof of shipped capability." — this is the **2nd-occurrence promotion** of the same failure mode and is now justified.
- **Promote?**: YES — promote on next commit. Target: append to [pre-edit.instructions.md](../instructions/pre-edit.instructions.md) "Pre-Edit Checklist" as a new bullet under or alongside step 0/1. The Phase 3 hooks entry above (2026-05-27 implementer / context-mode Phase 3 hooks) covers the closely related "probe CLI commands" rule — both should land in the same pre-edit amendment.
- **Status**: Resolved (doc fix shipped); **Promoted → [pre-edit.instructions.md](../instructions/pre-edit.instructions.md) "Verify external-tool capabilities empirically before documenting them" (same commit).**

## 2026-05-27 — Implementer / Phase 9 `domain-policy` CLI (file-first design)

- **Context**: ADR-0029 Phase 9 was originally scoped as "auto-load personal-overrides.local.txt from ootstrap.ps1". After design discussion (2026-05-27), repurposed as a standalone CLI for team-wide filter management and personal-overrides target was dropped entirely.
- **Decision**: Ship `scripts/adguard/domain-policy.ps1` + `.sh` parity with file-first workflow. Two targets only: `blacklist` (team-blacklist.txt, id=1001) and `whitelist` (team-whitelist.txt, id=1002). Subcommands: `status`, `show`, `edit`, `import`, `add`, `reload`, `help`. Edits HOST files only (volume-mounted); reload via `docker compose restart adguard` (~5s downtime, no credentials, no API token, no UI clicks).
- **Rationale**: (a) Separation of concerns — `bootstrap.ps1` owns user/yaml/container lifecycle (destructive, infrequent), `domain-policy.ps1` owns filter content (safe, frequent). Mixing risks accidental container recreate. (b) File-first beats string-first because real workflows are bulk edits (threat feeds, audit) — `edit` and `import` are primary; `add` is a one-off convenience. (c) Reload chosen over hot-reload because AdGuard v0.107.50 does not reliably hot-reload file-based filters without a kick; 5s downtime is acceptable for dev sandbox. (d) Dedup is intentionally narrow (exact text match, case-sensitive, comment-skipping) — no semantic dedup (`||evil.com^` vs `||www.evil.com^` are distinct rules; merging would silently drop coverage); no cross-file dedup (whitelist overriding blacklist is the legitimate AdGuard precedence pattern).
- **Personal-overrides target dropped (was v2)**: would have required touching bootstrap.ps1 + yaml template + a third CLI target for a per-developer use case that has not appeared in practice. Any rule a dev needs locally can be added to team-whitelist via PR (becomes permanent) or via `domain-policy.ps1 add` and reverted later. The `personal-overrides.local.example.txt` placeholder + `.gitignore` entry remain in place from Phase 5 for future revisit.
- **Action**: Documented in `docker/adguard/README.md` (new section "Daily management with the `domain-policy` CLI" including dedup-limitations table), `docs/getting-started-context-mode.md` (daily-life table updated), `docs/roadmap/context-mode-integration.md` (Phase 9 v1 ✅ Done + Phase 9 v2 NOT PLANNED block). VS Code tasks added: `AdGuard: Show all filters` / `AdGuard: Reload filters`.
- **Promote?**: NO — one-off implementation decision specific to ADR-0029 Phase 9. Covered by ADR-0029. No recurring failure mode to capture.
- **Status**: Resolved.

## 2026-05-27 - Implementer / Phase 7 L2 query_docs_cached (option C, Python-only)

- **Context**: After Phase 6 L1 shipped, three architectural options for L2 were on the table: (A) direct cross-MCP SQLite write from RAG server to context-mode FTS5 store, (B) file-staging via shared volume, (C) wrapper returns formatted markdown + deterministic source label, agent makes the follow-up ctx_index call. User chose option C explicitly with the rationale: minimal complexity, 50% gain at low cost is success.
- **Decision**: Ship `query_docs_cached` on the Python RAG server only. .NET parity deferred -- requires extending `ReadDocsChunk` Core record with `Breadcrumb` and `EndLine` properties, a bigger Core change than the wrapper itself, and the agent uses the Python server by default so practical impact is limited. Manual L1 path stays as canonical fallback for .NET.
- **Rationale**: (a) Option C has zero cross-MCP coupling: no new inter-server channels, no shared filesystem semantics, no SQLite write contention. (b) Wrapper logic is pure formatting + label derivation (about 150 LOC including tests). (c) Cache shape identical to L1 so both interoperate; recalls hit either kind via the same `source="rag-cache-"` prefix. (d) Source-label derivation: question contains ADR id (regex `\d{3,4}`) -> `rag-cache-adr<NNNN>-<hash8>`; else `bc=` set -> `rag-cache-<slug(bc)>-<hash8>`; else `rag-cache-q-<hash8>`. `<hash8>` = first 8 chars of sha256 of normalized question, making same (question, bc) -> same label -> idempotent overwrite.
- **Action**: Added `_tool_query_docs_cached` plus helpers `_derive_source_label` and `_format_chunks_to_markdown` to `tools/rag/rag_tools.py`. Registered in `tools/rag/mcp_server.py` `_TOOL_DISPATCH` and `list_tools()`. Wrote 9 unit tests in `tools/rag/tests/test_query_docs_cached.py` (all passing; full suite 293 passed, no regression). Updated `.github/instructions/mcp-routing.instructions.md` (L2 canonical, L1 fallback) and `.github/skills/rag-with-memory/SKILL.md` (L2 flow first, L1 demoted). Roadmap Phase 7 marked Done with 7.3 .NET parity deferred.
- **Promote?**: NO -- concrete implementation choice specific to one wrapper tool. No recurring failure mode to capture in anti-patterns.
- **Status**: Resolved.

## 2026-05-27 - Implementer / Phase 7.3 .NET parity for query_docs_cached

- **Context**: User asked to close the .NET gap left after the Python-only L2 ship. Blocker was that Core `DocumentSearchResult` / `SearchHit` / `QueryHit` exposed `StartLine` but not `EndLine`, so the .NET projector could not emit the `#L<start>-L<end>` path range the Python markdown uses.
- **Decision**: Thread `EndLine` through the three Core records (already stored in Qdrant payload as `end_line`; just unread). Add `QueryDocsCachedFormatter` (pure static class, mirrors Python `_derive_source_label` + `_format_chunks_to_markdown`), `RagToolsProjector.ProjectQueryCached`, and `RagTools.QueryDocsCached` MCP tool method. No new services / requests / outcomes -- reuse `IRagQueryService.QueryAsync` and format on top.
- **Rationale**: (a) `EndLine` is already in the Qdrant payload (`QdrantStore.UpsertChunksAsync` line 84). Reading it is one extra `TryGetValue` per hit; cost is trivial. (b) Adding a dedicated `IRagQueryDocsCachedService` mirroring the other four services was rejected -- it would duplicate request validation and embedding-call orchestration for a pure formatting concern. (c) `top_k` capped at `RagQueryService.MaxTopK = 20` (vs Python's `max(30, top_files*15) = 45`). Documented compromise: same label format, same markdown shape, slightly lower chunk density per file. Bumping MaxTopK would affect existing `query_docs` callers; not worth it.
- **Action**: Added `EndLine` to `SearchHit`, `DocumentSearchResult`, `QueryHit`; threaded through `QdrantStore.SearchAsync`, `QdrantDocumentStore.SearchAsync`, `RagQueryService.BuildResponse`. Added `tools/rag-dotnet/src/RagTools.Mcp/Tools/QueryDocsCachedFormatter.cs`. Added `ProjectQueryCached` to `RagToolsProjector`. Added `QueryDocsCached` `[McpServerTool]` on `RagTools.cs`. Added 14 pinning tests in `tools/rag-dotnet/src/RagTools.Tests/Tools/QueryDocsCachedFormatterTests.cs`. Existing constructor sites in tests updated for the new positional field. Full suite: 492 passed (was 478), build clean. Updated `mcp-routing.instructions.md`, `rag-with-memory` SKILL.md, and roadmap Phase 7.3 to mark parity shipped.
- **Promote?**: NO -- specific implementation choice for one wrapper port. The pattern (Core record extension + thin formatter + projector entry) is already documented in `dotnet.instructions.md` for the four existing tools.
- **Status**: Resolved.
