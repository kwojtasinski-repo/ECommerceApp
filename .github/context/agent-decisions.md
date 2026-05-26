# Agent Decisions Log

> **Append-only log of in-session corrections to AI agent behavior.**
> Operational, not architectural. For _why the system is built that way_ › ADRs in `docs/adr/`.
> For _how the agent should behave / what it missed / what to never repeat_ › here.
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

**Promotion rule**: if the same correction appears **2+ times** › promote to a permanent rule:

- Architectural rule › `anti-patterns-critical.context.md` or relevant `*.instructions.md`
- Workflow rule › relevant agent file (`bc-switch.md`, `code-reviewer.md`, etc.)
- Decision-level rule › new ADR via `@adr-generator`

When promoted, mark the entry **Status: Promoted › ADR-NNNN** (or file ref) and keep it for history.

---

## Entry format (Variant A — required)

```markdown
## YYYY-MM-DD — <agent-name> / <area>

- **Context**: What the agent tried to do.
- **Decision**: What the human decided instead (NO / YES / different approach).
- **Rationale**: Why — link to project-state line, ADR, instruction file, or commit.
- **Action**: What changes to instructions/agents/skills should follow (one concrete action).
- **Promote?**: When does this graduate to a permanent rule (e.g. "after 2nd occurrence › anti-patterns-critical").
- **Status**: Open | Resolved | Promoted › <ref>
```

Rules:

- One H2 per entry. **Append, do not edit history**.
- Date in `YYYY-MM-DD` format (today's real date).
- Keep entries scannable — 5–10 lines each. Link, don't quote.

---

## 2026-04-27 — Copilot / RAG MCP server config location

- **Context**: Agent created `.github/copilot/mcp.json` to register the RAG MCP server, then told the user the server was registered. VS Code's MCP browser showed no servers.
- **Decision**: The correct location is `.vscode/mcp.json`. `.github/copilot/mcp.json` is not read by VS Code's MCP server browser — it is only relevant for GitHub Codespaces / future GitHub Copilot tooling.
- **Rationale**: VS Code reads workspace MCP config from `.vscode/mcp.json`. The `.github/copilot/` path has no VS Code runtime effect.
- **Action**: Always create `.vscode/mcp.json` for VS Code MCP registration. Keep `.github/copilot/mcp.json` as a secondary copy for Codespaces compatibility only.
- **Promote?**: After 2nd occurrence › add to `docs-index.instructions.md` or a tooling note.
- **Status**: Resolved
- All entries in **English** for AI parsability.

---

## Example entry (template — replace with real ones)

## 2026-04-21 — bc-switch / Sales/Payments

- **Context**: Agent attempted to delete `Application/Services/Payments/PaymentHandler.cs` as part of the atomic switch.
- **Decision**: Do NOT delete during the switch.
- **Rationale**: `project-state.md` notes "Legacy `PaymentHandler` retained for Step 5 cleanup". The agent skipped reading project-state and assumed atomic switch implies delete-all-legacy.
- **Action**: Strengthen `bc-switch.md` Step 1 to require quoting the project-state line for the BC before any delete operation.
- **Promote?**: After 2nd occurrence › add explicit anti-pattern to `anti-patterns-critical.context.md` ("No legacy delete without project-state quote").
- **Status**: Open

---

## Entries

<!-- Append new entries below this line, newest at the bottom. -->

## 2026-05-18 — Implementer / RAG .NET configuration discovery

- **Context**: While stabilising `tools/rag-dotnet` for local dev, the plan referenced a non-existent `tools/rag-dotnet/rag-config.yaml` and a Python-venv `optimum-cli` step. Both wrong.
- **Decision**: (1) The .NET path **shares** `tools/rag/rag-config.yaml` with Python — Dockerfile literally does `COPY ../rag/rag-config.yaml /app/rag-config.yaml`. No separate .NET config exists. (2) The HuggingFace ONNX bundle (`/onnx/model.onnx` + `vocab.txt` + `tokenizer.json` + `config.json`) is pre-exported by sentence-transformers maintainers, so a PowerShell/curl download replaces the Python optimum-cli stage entirely.
- **Rationale**: Source of truth verified in `tools/rag-dotnet/Dockerfile` line ~45 and HuggingFace repo for `paraphrase-multilingual-MiniLM-L12-v2`.
- **Action**: `RagConfig.ResolveConfigPath` uses 4-way priority: explicit arg › `RAG_CONFIG` › `RAG_WORKSPACE`-derived `<ws>/tools/rag/rag-config.yaml` › `AppContext.BaseDirectory/rag-config.yaml`. `RagConfig.Workspace` derives from config-path grandparent (Python parity with `config_path.parents[2]`), then `RAG_WORKSPACE`, then cwd. Local devs run `pwsh tools/rag-dotnet/download-model.ps1` once; Docker uses `curlimages/curl` stage. **Never invent `tools/rag-dotnet/rag-config.yaml` again.**
- **Status**: Resolved

---

## 2026-05-19 — Implementer / RAG tool and test self-containment

- **Context**: When implementing .NET e2e tests the agent initially considered referencing real EcommerceApp ADR numbers and domain entities (e.g. `CustomerId`, `ADR-0016`) in the synthetic test workspace.
- **Decision**: Both RAG implementations (Python `tools/rag/` and .NET `tools/rag-dotnet/`) must be **self-contained**. Tests must use a synthetic workspace with domain-neutral content (no EcommerceApp ADR numbers, entity names, or bounded-context identifiers). The workspace is created in a temp directory with a UUID-suffixed collection name. See ADR-0027 §9 for the full rule.
- **Rationale**: Self-containment lets the RAG tooling be reused in other projects without modification and lets CI run e2e tests without a full repo checkout.
- **Action**: When writing or reviewing RAG tests: reject any fixture that imports real docs, real ADR paths, or real entity names from EcommerceApp. `SyntheticWorkspace.cs` and `conftest.py` are the approved patterns.
- **Promote?**: After 2nd drift › add to `dotnet.instructions.md` under RAG test rules.
- **Status**: Resolved

---

## 2026-05-19 — Implementer / RAG multilingual glossary expansion

- **Context**: After the multilingual glossary was added (one-shot append), Polish and German "known issues FluentAssertions" queries still returned the wrong document at #1. Mean pooling gives equal weight to every token — a 7-word PL/DE query with a 10-word English expansion appended once yields only ~33–37% English weight, insufficient to overcome the semantic pull of generic words like `Fehler`/`b³êdy` toward unrelated error-handling docs.
- **Decision**: Repeat the English expansion **3 times** (not replace the non-English words — too aggressive). `return query + (" " + expansion) * 3` in Python; `Enumerable.Repeat(" " + expansion, 3)` in .NET. This raises English weight to 60–87% for typical short queries.
- **Rationale**: Mean pooling is linear: token count drives weight. Repetition is the cheapest, safest amplification — it degrades gracefully (English-only queries are never touched because only non-ASCII patterns fire) and requires no model change or re-index.
- **Pitfall 1 — `@dataclass` silently dropped**: When inserting `_expand_query` and `QueryHit` as a file-level replacement, the `@dataclass` decorator was inadvertently omitted from `QueryHit`. The class appeared valid (field annotations existed) but calling `QueryHit(rel_path=..., ...)` raised `TypeError: QueryHit() takes no arguments`. Fix: always read the full class definition before replacing; never omit decorators during partial replacements.
- **Pitfall 2 — `_glossary` missing after `__new__`**: `make_engine_with_stubs()` in tests uses `QueryEngine.__new__()` to bypass `__init__`, so the `engine._glossary = []` assignment inside `__init__` was skipped › `AttributeError`. Fix: after any `__init__` addition, check all `__new__`-based test factories and add the new attribute.
- **Pitfall 3 — .NET MCP build lock**: `dotnet build` while VS Code holds the .NET MCP server process running will fail (DLLs locked). **Always warn the user** before running `dotnet build` on any project whose output is an active MCP server; ask them to stop the server first.
- **Action**: §10 added to ADR-0027; repeat=3 documented; glossary gap for `Bezeichner` (DE entity identifier) added to both glossary YAMLs; conformance checklist items added.
- **Status**: Resolved

---

## 2026-05-23 — RAG SSE ingest pipeline stabilisation session

### ZIP validation (upload endpoint)

- **Decision**: Every POST to `/ingest/{collection}/batch` must contain `metadata-rules.yaml` AND `queries.yaml` inside the ZIP. Missing either › 400. Both files are parsed and validated (non-empty `doc_kind_rules`, non-empty `named_queries`, cross-validated `doc_kind` vocabulary). Config files are then filtered out before ingest — they are not indexed as documents.
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
