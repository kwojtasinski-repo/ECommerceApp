# Copilot Setup — Changelog & Current State

> This file tracks **what changed** in the `.github/` Copilot configuration and serves
> as a snapshot of the current setup. Updated by `@copilot-setup-maintainer` or manually.
>
> Use as a quick reference to see what exists without scanning every file.

---

## Current state summary

| Category                               | Count | Details                                                                                                                            |
| -------------------------------------- | ----- | ---------------------------------------------------------------------------------------------------------------------------------- |
| `copilot-instructions.md`              | 1     | ~7 410 chars (under 8K soft budget; Session 26 trim from 11 975); §12/§14 collapsed to pointers, dedicated `batched-tasks.instructions.md` added |
| Instruction files (`.instructions.md`) | 17    | All with `applyTo:` frontmatter; +`batched-tasks` (Session 26); `agent-memory` added; `pre-edit` split into core + `doc-suggestions`; `docs-index` scope narrowed |
| Prompt files (`.prompt.md`)            | 6     | BC analysis, BC implementation, PR review, refactor, flow-analysis, rag-sync                                                        |
| Agent files                            | 8     | adr-generator, bc-switch, code-reviewer, copilot-setup-maintainer, planner, implementer, verifier, pr-commit                       |
| Skills (`SKILL.md`)                    | 17    | +rag-with-memory (Session 26); +5 RAG skills (Session 23): diagnose-rag, tune-rag-weights, expand-rag-glossary, generate-rag-rules, generate-eval-questions |
| ADRs                                   | 29    | Folderized ADR routers under `docs/adr/<NNNN>/README.md`; ADR-0027/0028 (RAG pipeline) + ADR-0029 (context-mode sandbox) added                                                                           |
| Context files                          | 6     | project-state, known-issues, agent-decisions, repo-index, future-skills, anti-patterns-critical                                    |
| GitHub Actions workflows               | 1     | `dotnet-ci.yml` — manual trigger only (push/PR commented)                                                                          |
| Pipeline orchestration spec            | 1     | `.github/AGENT-PIPELINE.md`; `@verifier`/`@code-reviewer` embedded inside `@implementer`; standalone-only for one-off use          |
| HTTP scenario files                    | 10    | +auth.http (was 9)                                                                                                                 |
| Test files                             | 135   | 94 unit + 41 integration                                                                                                           |

---

## File inventory

### `.github/instructions/` (17 files)

| File                                  | `applyTo:`                                                    | Added                                                                       |
| ------------------------------------- | ------------------------------------------------------------- | --------------------------------------------------------------------------- |
| `dotnet.instructions.md`              | `**/*.cs, **/*.csproj`                                        | Session 1 (renamed)                                                         |
| `efcore.instructions.md`              | `ECommerceApp.Infrastructure/**/*.cs, **/*.csproj`            | Session 1 (renamed)                                                         |
| `frontend.instructions.md`            | `ECommerceApp.Web/wwwroot/**, **/*.cshtml`                    | Session 1 (renamed)                                                         |
| `razorpages.instructions.md`          | `ECommerceApp.Web/**/*.cshtml, **/*.cshtml.cs, **/*.cs`       | Session 1 (renamed)                                                         |
| `web-api.instructions.md`             | `ECommerceApp.API/**/*.cs`                                    | Session 1 (renamed)                                                         |
| `testing.instructions.md`             | `ECommerceApp.UnitTests/**, ECommerceApp.IntegrationTests/**` | Session 1 (renamed)                                                         |
| `migration-policy.instructions.md`    | `ECommerceApp.Infrastructure/Migrations/**`                   | Session 1 (renamed)                                                         |
| `shared-primitives.instructions.md`   | `ECommerceApp.Domain/Shared/**/*.cs`                          | Pre-existing                                                                |
| `safety.instructions.md`              | `**`                                                          | Session 1 (extracted from copilot-instructions)                             |
| `pre-edit.instructions.md`            | `**`                                                          | Session 1 (extracted from copilot-instructions)                             |
| `docs-index.instructions.md`          | `.github/**, docs/**`                                         | Session 1 (new — docs lookup table); Session 19 (scope narrowed from `**`)  |
| `copilot-config-sync.instructions.md` | `.github/**, docs/**`                                         | Session 11 (new); Session 14 (extended to docs/\*\*)                        |
| `rag.instructions.md`                 | `**`                                                          | Session 18 (new — RAG routing precedence + output discipline)               |
| `bc-adr-map.instructions.md`          | `**/*.cs, **/*.csproj, **/*.cshtml`                           | Session 19 (new — BC → ADR quick map for code edits)                        |
| `doc-suggestions.instructions.md`     | `**`                                                          | Session 19 (new — proactive doc suggestion triggers; split from `pre-edit`) |
| `agent-memory.instructions.md`        | `**`                                                          | Session 19 (new — auto-loads `agent-decisions.md` read rule on every task)  |
| `batched-tasks.instructions.md`       | `**`                                                          | Session 26 (new — extracted from `copilot-instructions.md` §14 to keep auto-load while shrinking the root policy file) |

### `.github/prompts/` (6 files)

| File                          | Added                                                                                                                                   |
| ----------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| `bc-analysis.prompt.md`       | Session 1 (renamed)                                                                                                                     |
| `bc-implementation.prompt.md` | Session 1 (renamed)                                                                                                                     |
| `pr-review.prompt.md`         | Session 1 (renamed)                                                                                                                     |
| `refactor.prompt.md`          | Session 17 (new)                                                                                                                        |
| `flow-analysis.prompt.md`     | Session 22 (registered — file existed on disk, referenced in `copilot-instructions.md` §11 and `docs-index`, omitted from prior counts) |
| `rag-sync.prompt.md`          | Session 23 (registered — file existed on disk, runs incremental ingest + eval validation + coverage check)                             |

### `.github/agents/` (8 files)

| File                          | Added                                                                                                   |
| ----------------------------- | ------------------------------------------------------------------------------------------------------- |
| `adr-generator.md`            | Pre-existing (Session 17: HITL before write + max-iter 2)                                               |
| `bc-switch.md`                | Pre-existing (Session 17: HITL after Step 1, before Step 6 + max-iter 10)                               |
| `code-reviewer.md`            | Session 3 (Session 17: pipeline awareness + refactor detection + max-iter 3, agent-decisions awareness) |
| `copilot-setup-maintainer.md` | Session 1 (Session 17: Workflow 12 pipeline orchestration sync + AGENT-PIPELINE ownership)              |
| `planner.md`                  | Session 17 (new — pipeline stage 1, HITL CHECKPOINT 1, max-iter 3)                                      |
| `implementer.md`              | Session 17 (new — pipeline stage 2, scope-limited, max-iter 5)                                          |
| `verifier.md`                 | Session 17 (new — pipeline stage 3, deterministic build+test, max-iter 1)                               |
| `pr-commit.md`                | Session 17 (new — pipeline stage 5, Conventional Commits, max-iter 2)                                   |

### `.github/skills/` (17 skills)

| Skill                     | Description                                                     | Added      |
| ------------------------- | --------------------------------------------------------------- | ---------- |
| `create-unit-test`        | xUnit test class (service, aggregate, handler)                  | Session 2  |
| `create-dbcontext`        | Per-BC DbContext (4-file scaffold)                              | Session 2  |
| `create-ef-configuration` | EF Core entity configuration                                    | Session 2  |
| `create-di-extension`     | Application + Infrastructure DI extensions                      | Session 2  |
| `create-domain-event`     | Cross-BC IMessage + IMessageHandler (3 modes)                   | Session 2  |
| `create-integration-test` | Integration test with BaseTest + Shouldly                       | Session 2  |
| `create-http-scenario`    | .http file for any API endpoint testing                         | Session 2  |
| `create-validator`        | FluentValidation AbstractValidator                              | Session 2  |
| `create-cqrs-handler`     | ICommandHandler<TCommand,TResult> + command + result (Option B) | Session 20 |
| `create-dto-viewmodel`    | DTO/VM + ToDto() extension method (AutoMapper removal path)     | Session 20 |
| `create-message-contract` | Cross-BC IMessage event contract, publisher side only           | Session 20 |
| `generate-eval-questions` | Eval question template for newly indexed RAG docs               | Session 23 |
| `diagnose-rag`            | MCP diagnostic playbook — 7 failure categories                  | Session 23 |
| `tune-rag-weights`        | Adjust rag-config.yaml ranking weight multipliers                   | Session 23 |
| `expand-rag-glossary`     | Add PL/DE patterns to multilingual-glossary.yaml                | Session 23 |
| `generate-rag-rules`      | Update metadata-rules.yaml and queries.yaml                     | Session 23 |
| `rag-with-memory`         | RAG ↔ context-mode FTS5 handoff (L1 manual, 3-call) walkthrough | Session 26 |

### `.github/context/` (6 files)

| File                                | Added                                                |
| ----------------------------------- | ---------------------------------------------------- |
| `project-state.md`                  | Pre-existing                                         |
| `known-issues.md`                   | Pre-existing                                         |
| `agent-decisions.md`                | Session 17 (new — append-only agent corrections log) |
| `repo-index.md`                     | Session 2 (new — full codebase map)                  |
| `future-skills.md`                  | Session 2 (new — skills roadmap)                     |
| `anti-patterns-critical.context.md` | Session 3 (renamed from anti-patterns.context.md)    |

---

## Change log

### Session 30 — RAG auto-cache hook (L3 — host-side PostToolUse) (2026-05-28)

Ships an automated RAG → context-mode FTS5 cache pipeline. The agent now makes **one** RAG call instead of two (no more manual `ctx_index` follow-up). Implemented as a host-side PostToolUse hook in the Copilot Chat hook chain, not inside the context-mode sandbox. Tool detection uses **runtime introspection** of `.vscode/mcp.json` (1h cache, lock-coalesced) so new RAG tools are auto-discovered without code changes.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | New host-side hook entry `auto-cache.mjs`: detects RAG MCP calls (runtime-discovered tool list with hardcoded fallback), shape-driven markdown formatter (handles `files[].chunks[]`, `hits[]`, `results[]`), one-shot `ctx_index` via MCP stdio. Uses `rag-auto-` source prefix; recallable alongside `rag-cache-` (L2) via `ctx_search(source="rag-")`. Probe suite `auto-cache.probes.mjs` covers JSONC parser (incl. URL preservation), shape-driven formatter, source-label hash, and lock-file TTL. | `.github/hooks/auto-cache.mjs` _(new)_, `.github/hooks/auto-cache.probes.mjs` _(new)_ |
| 2 | New fan-out wrapper `posttooluse-chain.ps1`: pipes hook envelope to BOTH the upstream context-mode hook AND `auto-cache.mjs`. Best-effort, always `exit 0`. Logging gated on `AUTO_CACHE_DEBUG` (default ON). | `.github/hooks/posttooluse-chain.ps1` _(new)_ |
| 3 | Hook config wires PostToolUse to the PS wrapper (other 4 hooks unchanged). | `.github/hooks/context-mode.json` |
| 4 | New ADR amendment documenting decision, architecture, runtime introspection, source labels, latency, and conformance checklist. | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` _(new)_, `docs/adr/0029/README.md` |
| 5 | New operator guide covering pipeline, file layout, discovery mechanism, source labels, debug switch, troubleshooting, limitations, and performance. | `docs/rag/auto-cache-hook.md` _(new)_ |
| 6 | Routing rules — new "L3 default" section as canonical handoff. L2 demoted to "still valid", L1 demoted to "fallback only". Note added that the explicit `ctx_index` is no longer required under L3. | `.github/instructions/mcp-routing.instructions.md` |
| 7 | Agent-decisions entries: (a) L3 hook pattern, (b) shape-driven formatter beats tool-name dispatch, (c) JSONC parser must be string-aware. | `.github/context/agent-decisions.md` |
| 8 | Runtime files (`.rag-tools-cache.json`, `.rag-tools-cache.lock`, `auto-cache.log`) added to .gitignore. | `.gitignore` |

**Empirical verification (this session)**:

- Stdio introspection found 4 RAG servers in `mcp.json` and merged tools to `{get_history, list_adrs, query_docs, query_docs_cached, read_docs}` (5 tools, `source=introspected`).
- Lock coalescing: 3 concurrent kickoffs during a 30s discovery window → only 1 background process spawned; lock removed at completion.
- End-to-end recall verified: `ctx_search(source="rag-auto-")` returns full chunk text from the most recent `query_docs` calls.
- Per-fire latency measured at 290–310ms (PS + Node + docker exec).

**Not changed (deliberate)**:

- No new instruction, prompt, agent, or skill **files**. Counts: instructions still 17, agents still 8, skills still 17, ADRs now 29 (Amendment 1 lives inside ADR-0029 folder).
- `ECommerceApp.sln` — hook scripts are runtime config, no project membership.

### Session 28 — Phase 7 L2: `query_docs_cached` wrapper (2026-05-27)

Collapses the manual RAG → context-mode handoff (3 steps) into 1 + 1: a single `query_docs_cached` call returns formatted markdown + a deterministic source label; the agent makes one pass-through `ctx_index` call. Architecture choice **option C** (return-and-let-caller-cache) — no cross-MCP coupling, no shared-volume staging, no direct SQLite writes. Python RAG server only; `.NET` parity deferred (requires Core data-model change). Cache shape identical to L1 → both interoperate.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | New MCP tool `query_docs_cached` — handler `_tool_query_docs_cached` + helpers `_derive_source_label`, `_format_chunks_to_markdown`. Source label: ADR id detected → `rag-cache-adr<NNNN>-<hash8>`; `bc=` set → `rag-cache-<slug(bc)>-<hash8>`; fallback → `rag-cache-q-<hash8>`. `<hash8>` = first 8 chars sha256(question.lower().strip()), so re-running with same `(question, bc)` overwrites idempotently. | `tools/rag/rag_tools.py`, `tools/rag/mcp_server.py` |
| 2 | 9 unit tests covering label routing (ADR/BC/fallback), determinism, ASCII/kebab/case rules, markdown shape (header, source line, path-with-line-range, breadcrumb, multi-file). Full suite **293 passed**, no regression. | `tools/rag/tests/test_query_docs_cached.py` _(new)_ |
| 3 | Routing rules — new "L2 fast path" section as canonical handoff; old manual 3-step path demoted to "fallback / .NET server only". Source-label rules table updated to match wrapper's derivation. | `.github/instructions/mcp-routing.instructions.md` |
| 4 | Skill `rag-with-memory` reframed: L2 flow first (preferred), L1 manual flow demoted to fallback. Headline + intro rewritten; new "Preferred flow (L2)" diagram block. | `.github/skills/rag-with-memory/SKILL.md` |
| 5 | Roadmap Phase 7 moved from "future" to ✅ Done (option C, Python-only). Steps 7.1, 7.2, 7.4, 7.5, 7.6, 7.7, 7.8 done with file links; step 7.3 (.NET parity) marked Deferred with rationale (needs `ReadDocsChunk.Breadcrumb` + `EndLine`). | `docs/roadmap/context-mode-integration.md` |
| 6 | Agent-decisions entry "2026-05-27 — Implementer / Phase 7 L2 `query_docs_cached` (option C, Python-only)" with full design rationale, label-derivation spec, and scope. Promote=NO (one-off implementation choice). | `.github/context/agent-decisions.md` |

**Not changed (deliberate)**:

- `docs-index.instructions.md` — `query_docs_cached` is one more tool inside the existing RAG MCP routing target; no new routing target.
- `ECommerceApp.sln` — Python wrapper, no project membership.
- No new instruction, prompt, agent, or ADR files; current-state counts unchanged.

**Pre-existing drift (still open)**: ~~`.NET` RAG server lacks `query_docs_cached`~~ — closed in Session 28.1.

**Refs**: commit `d2f054dc`, `docs/roadmap/context-mode-integration.md` Phase 7.

---

### Session 28.1 — Phase 7.3: `.NET` parity for `query_docs_cached` (2026-05-27)

Closes the `.NET` gap left in Session 28. `RagTools.Mcp` now exposes `QueryDocsCached` with byte-identical source labels and markdown shape to the Python wrapper. Threaded `EndLine` through the three Core records (already in Qdrant payload as `end_line` — just wasn't read). Pure-formatter + projector pattern, no new application service.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Core records gain `EndLine`: `SearchHit`, `DocumentSearchResult`, `QueryHit`. Read from Qdrant `end_line` payload in `QdrantStore.SearchAsync`; threaded through `QdrantDocumentStore.SearchAsync` and `RagQueryService.BuildResponse`. Projector emits `end_line` in `ProjectQuery` JSON. | `tools/rag-dotnet/src/RagTools.Core/QdrantStore.cs`, `IDocumentStore.cs`, `QdrantDocumentStore.cs`, `Query/QueryOutcome.cs`, `Query/RagQueryService.cs`, `RagTools.Mcp/Tools/RagToolsProjector.cs` |
| 2 | New static `QueryDocsCachedFormatter` — pure port of Python `_derive_source_label` + `_format_chunks_to_markdown` + group-by-file + top-5-per-file logic. Returns `CachedPayload` with snake_case `ToProjection()` helper. | `tools/rag-dotnet/src/RagTools.Mcp/Tools/QueryDocsCachedFormatter.cs` _(new)_ |
| 3 | New `[McpServerTool] QueryDocsCached` on `RagTools.cs` — params `(question, bc?, top_files?)`. Reuses `IRagQueryService.QueryAsync`. `top_k` clamped to `RagQueryService.MaxTopK` (20) instead of Python's `max(30, top_files*15)` — documented compromise; same label format and markdown shape. | `tools/rag-dotnet/src/RagTools.Mcp/Tools/RagTools.cs` |
| 4 | New `ProjectQueryCached` in projector. | `tools/rag-dotnet/src/RagTools.Mcp/Tools/RagToolsProjector.cs` |
| 5 | 14 pinning tests for `QueryDocsCachedFormatter` (label routing, determinism, kebab/ASCII, markdown shape, group-by-file ranking, 5-chunk cap, snake_case projection). Full `.NET` suite: **492 passed** (was 478), build clean, no regressions. Existing `Hit()` test factories updated for the new positional `EndLine` field. | `tools/rag-dotnet/src/RagTools.Tests/Tools/QueryDocsCachedFormatterTests.cs` _(new)_, `RagToolsProjectorTests.cs`, `Query/QueryOutcomeTests.cs`, `Query/QueryOutcomeExtensionsTests.cs`, `Query/RagQueryServiceTests.cs`, `History/RagHistoryServiceTests.cs`, `ReadDocs/RagReadDocsServiceTests.cs` |
| 6 | Routing rules — Availability note updated: both servers now expose L2; `.NET` limitation is the `top_k` cap, not absence of the tool. | `.github/instructions/mcp-routing.instructions.md` |
| 7 | Skill `rag-with-memory` — drop "L1 only on `.NET`" language. L1 is the timeout/error fallback for both servers. | `.github/skills/rag-with-memory/SKILL.md` |
| 8 | Roadmap Phase 7.3 flipped from Deferred to ✅ Done with file links. | `docs/roadmap/context-mode-integration.md` |
| 9 | Agent-decisions entry "2026-05-27 — Implementer / Phase 7.3 .NET parity for `query_docs_cached`" with rationale for `EndLine` threading vs Core data-model larger change. Promote=NO. | `.github/context/agent-decisions.md` |

**Not changed (deliberate)**:

- No new application service / request / outcome — wrapper is pure formatting on top of `IRagQueryService.QueryAsync`. Adding `IRagQueryDocsCachedService` would duplicate request validation for no behavioural difference.
- `RagQueryService.MaxTopK` unchanged at 20 — bumping it would affect existing `query_docs` callers.
- `docs-index.instructions.md` — `QueryDocsCached` is one more tool inside the existing RAG MCP routing target.
- `ECommerceApp.sln` — `.NET` RAG server is in its own `tools/rag-dotnet/RagTools.sln`.

**Pre-existing drift**: none introduced.

**Refs**: `docs/roadmap/context-mode-integration.md` Phase 7.3, `tools/rag-dotnet/src/RagTools.Mcp/Tools/QueryDocsCachedFormatter.cs`.

---

### Session 27 — Phase 9 v1: AdGuard `domain-policy` CLI (2026-05-28)

File-first CLI for managing AdGuard team filter lists (blacklist/whitelist) without touching `bootstrap.ps1`, the AdGuard API, or the web UI. Edits host-mounted `team-blacklist.txt` / `team-whitelist.txt` directly, reloads via `docker compose restart adguard` (~5 s downtime). Two-script parity (PowerShell + bash). Phase 9 v2 (personal-overrides target) explicitly dropped — rationale recorded in the roadmap.

| #   | Change                                                                                                                                                                                                                                                                                                       | Files affected                                                                          |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------- |
| 1   | New CLI: `domain-policy.ps1` (~330 lines) + `domain-policy.sh` parity — subcommands `status`, `show`, `edit`, `import`, `add`, `reload`, `help`; targets `blacklist` (id=1001) and `whitelist` (id=1002); host-file edits + container restart only (no API token, no credentials)                            | `scripts/adguard/domain-policy.ps1` _(new)_, `scripts/adguard/domain-policy.sh` _(new)_ |
| 2   | "Daily management with the `domain-policy` CLI" section added, including dedup-limitations table (exact text match, case-sensitive, comment-skipping)                                                                                                                                                        | `docker/adguard/README.md`                                                              |
| 3   | Daily-life table updated to route filter-edit tasks through the new CLI instead of UI / bootstrap                                                                                                                                                                                                            | `docs/getting-started-context-mode.md`                                                  |
| 4   | Roadmap: Phase 9 v1 ✅ Done block + new "Phase 9 v2 — NOT PLANNED" block (drops `personal-overrides` target; rationale: avoids bootstrap touch + yaml template + 3rd CLI target for a use case that has not appeared; existing `personal-overrides.local.example.txt` placeholder retained for future revisit) | `docs/roadmap/context-mode-integration.md`                                              |
| 5   | Agent-decisions entry "2026-05-27 — Implementer / Phase 9 `domain-policy` CLI (file-first design)" (Promote=NO; first occurrence)                                                                                                                                                                            | `.github/context/agent-decisions.md`                                                    |
| 6   | Local-only VS Code tasks "AdGuard: Show all filters" / "AdGuard: Reload filters" (`.vscode/tasks.json` is gitignored; noted for awareness)                                                                                                                                                                   | `.vscode/tasks.json` _(local, gitignored)_                                              |

**Not changed (deliberate)**:

- `docs-index.instructions.md` — CLI is operational tooling, not a knowledge-routing target. Reference docs are already RAG-indexed.
- `ECommerceApp.sln` — no precedent for `docker/` or `scripts/` folders in the solution; adding only `scripts/adguard/` would be an inconsistent partial mirror.
- No instruction, prompt, agent, skill, or ADR files added → current-state counts unchanged.

**Pre-existing drift (still open, flagged in Session 24)**: ADR-0027 and ADR-0028 not in `.sln`. Separate backfill close-out recommended.

---

### Session 26 — RAG ↔ context-mode L1 handoff (skill + parametric mount) (2026-05-27)

Ships L1 (documentation-only) for caching RAG knowledge in context-mode's FTS5 store across recalls. Validated end-to-end via 4 tests (1 primary-agent POC, 2 subagent diagnostics, 1 fresh-window user-driven). Confirmed hard surface restriction on built-in subagents (LIMIT-1 — inline-chunks pattern is the only workaround). Multilingual recall caveat documented after Test 4 exposed FTS5 Porter-stemmer gap. Workspace mount path de-hardcoded (parametric `${CONTEXT_MODE_WORKSPACE:-/workspace}`).

| #   | Change                                                                                                                                              | Files affected                                                                       |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| 1   | New skill `rag-with-memory` — step-by-step walkthrough of the manual 3-call handoff with Step 0 workspace probe, subagent inline-chunks pattern, multilingual recall caveat | `.github/skills/rag-with-memory/SKILL.md` (new)                                       |
| 2   | New routing section "RAG ↔ context-mode handoff (knowledge caching across recalls)" with three-surface disambiguation table + anti-patterns | `.github/instructions/mcp-routing.instructions.md`                                    |
| 3   | Pattern doc gained "Integration with RAG" section + "How to discover the workspace mount path" probe recipe; all `/workspace` hardcodes replaced with `$CONTEXT_MODE_WORKSPACE` | `docs/patterns/context-mode-read-write-split.md`                                      |
| 4   | Compose service mount target and env var both parametric: `${CONTEXT_MODE_WORKSPACE:-/workspace}` (forks override via `.env.context-mode`)         | `docker-compose.yaml`                                                                  |
| 5   | Roadmap Phase 7 (`query_docs_cached` wrapper) + new "L1 ship status & open follow-ups" section with LIMIT-1 (hard subagent restriction, CONFIRMED), LIMIT-2 (probe enforcement, deferred), LIMIT-3 (multilingual FTS gap, documented as caveat) | `docs/roadmap/context-mode-integration.md`                                            |
| 6   | Agent-decisions entry recording POC + 3 validation tests, decision, rationale, promotion triggers                                                   | `.github/context/agent-decisions.md`                                                  |
| 7   | docs-index gained row for the new skill                                                                                                            | `.github/instructions/docs-index.instructions.md`                                      |
| 8   | `copilot-instructions.md` trimmed 11 975 → 7 409 chars (-38%): §12 MCP routing collapsed to a short pointer (full rules already in `mcp-routing.instructions.md` with `applyTo: **`); §14 batched-tasks extracted to a dedicated `batched-tasks.instructions.md` (`applyTo: **`, behaviour preserved) | `.github/copilot-instructions.md`, `.github/instructions/batched-tasks.instructions.md` (new) |
| 9   | Maintainer ownership budget raised 4K → 8K chars (soft, with rationale) — the original 4K target was set in Session 17 when the file held ~3K of content and is no longer realistic given the current 14 sections + 4 domain constants + cross-link pointers. Refactor guidance added: move duplicates to `applyTo: **` instruction files instead of deleting unique policy. | `.github/agents/copilot-setup-maintainer.md`                                          |

---

### Session 25 — Multi-MCP routing rollout (Phases 1-5 + tightening) (2026-05-27)

Single sweep adapting the entire Copilot workflow to two coexisting MCPs (RAG + context-mode — **both live**; context-mode promoted from dormant→active after handshake verified end-to-end). Zero production code, zero migrations, zero tests touched — config only.

| #   | Change                                                                                                                                                                  | Files affected                                                                                                                                                                                  |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Phase 1 — Foundation**: new canonical single-source-of-truth (status tables, ASCII flow diagram, RAG + context-mode tool tables, 5 HARD precedence rules, trigger-phrase routing, fallback ladder). RAG instructions trimmed to RAG-only ops. `copilot-instructions.md` §12 collapsed to 6-bullet non-negotiable summary linking the canonical file. `docs-index` lifted MCP routing to top-of-table | `.github/instructions/mcp-routing.instructions.md` _(new)_, `.github/instructions/rag.instructions.md`, `.github/copilot-instructions.md`, `.github/instructions/docs-index.instructions.md` |
| 2   | **Phase 2 — Agents + pipeline**: per-agent MCP scopes added (Planner=RAG read-only; Implementer=RAG+`ctx_execute/_file/_fetch`; Verifier=**NONE** as hard rule; Code-reviewer=RAG read-only; PR-commit=`get_history` to verify ADR refs; BC-switch Step 0 RAG lookup; ADR-generator prefers `list_adrs`/`query_docs`/`get_history`). `AGENT-PIPELINE.md` max-iter table gained "MCP tools allowed" column. `copilot-setup-maintainer` now owns `mcp-routing.instructions.md` | `.github/agents/planner.md`, `implementer.md`, `verifier.md`, `code-reviewer.md`, `pr-commit.md`, `bc-switch.md`, `adr-generator.md`, `copilot-setup-maintainer.md`, `.github/AGENT-PIPELINE.md` |
| 3   | **Phase 3 — Prompts**: Step 0 MCP lookup added to BC-analysis, flow-analysis, PR-review, refactor (inside Pre-edit gate), BC-implementation (Step 0a). 16 skills intentionally skipped (5 already RAG-native; 11 creator-scaffolds don't read project knowledge at runtime — Planner/Implementer enforce routing before invoking them) | `.github/prompts/bc-analysis.prompt.md`, `flow-analysis.prompt.md`, `pr-review.prompt.md`, `refactor.prompt.md`, `bc-implementation.prompt.md`                                                  |
| 4   | **Phase 4 — Pre-edit + safety + memory + anti-patterns**: pre-edit checklist now MCP-first (prefer `query_docs`/`get_history`); URL handling rule (`ctx_fetch_and_index` only for project URLs); architecture-suggestion guard (`query_docs` for governing ADR first); safety adds external-HTTP and verifier-no-MCP rules; agent-memory adds pre-write `query_docs` dedupe check; anti-patterns adds 4 BLOCKS-MERGE rules (double-MCP, raw `fetch_webpage` for project URLs, training-data quotes, MCP-in-verifier) | `.github/instructions/pre-edit.instructions.md`, `safety.instructions.md`, `agent-memory.instructions.md`, `.github/context/anti-patterns-critical.context.md`                                  |
| 5   | **Phase 5 — Changelog + playbook retrospective**: this entry + playbook §14 case study with both commit SHAs (Phase 0 `cef1bca5` + this commit)                          | `.github/COPILOT-SETUP-CHANGELOG.md`, `docs/rag/mcp-first-routing-migration-playbook.md`                                                                                                        |

**Precedence rules (now canonical)**:

1. Knowledge → RAG. **`grep_search`/`read_file` on `.github/context/*.md`, `docs/adr/**`, `docs/roadmap/**`, `docs/architecture/bounded-context-map.md` before `query_docs`/`get_history` = BLOCKS MERGE.**
2. Sandboxed exec / large-file summary / hashes / math → context-mode (live; 11 tools: `ctx_execute`, `ctx_execute_file`, `ctx_index`, `ctx_search`, `ctx_fetch_and_index`, `ctx_batch_execute`, `ctx_stats`, `ctx_doctor`, `ctx_upgrade`, `ctx_purge`, `ctx_insight`).
3. External URL → `ctx_fetch_and_index` only.
4. Both empty → direct `read_file`/`grep_search` + name the failing MCP to the user.
5. **NEVER call both MCPs for the same atomic intent.**

**Tightening (post-test)**: after first test round revealed agent fell back to `grep_search` for "FluentAssertions known issue" and computed SHA-256 from training-data memory: (a) flipped context-mode from "dormant" to "live" across all gating clauses (7 files); (b) expanded context-mode tool table from 5→11 tools; (c) added explicit forbidden-paths list to rule #1 + new BLOCKS-MERGE anti-patterns for grep-before-MCP and training-data computation.

**Files NOT touched**: zero changes under `Application/`, `Domain/`, `Infrastructure/`, `Web/`, `API/`, tests, migrations. No package version changes. No `.vscode/mcp.json` edits (parallel chat owns context-mode registration).

**Skipped on purpose** (request follow-up if needed): 16 skills bulk pass; 5 skills under `.github/skills/` already wire to RAG; the 11 creator-scaffolds (`create-cqrs-handler`, `create-dbcontext`, `create-di-extension`, `create-domain-event`, `create-dto-viewmodel`, `create-ef-configuration`, `create-http-scenario`, `create-integration-test`, `create-message-contract`, `create-unit-test`, `create-validator`) operate post-routing.

### Session 24 — ADR-0029 context-mode MCP sandbox DRAFT + env knobs (2026-05-27)

| #   | Change                                                                                                                                                           | Files affected                                                                                    |
| --- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| 1   | ADR-0029 DRAFT introduced (prior commit `c663a08c`): docker-only context-mode sandbox, custom `ctx-net` bridge, AdGuard 4-file config (`AdGuardHome.yaml`, `team-blacklist.txt`, `team-whitelist.txt`, `personal-overrides.local.example.txt`), 6 hardening flags, `network-monitor.js` PreToolUse hook, Phase 6 future-policy work | `docs/adr/0029/0029-context-mode-mcp-sandbox.md`, `docs/adr/0029/README.md`                       |
| 2   | Roadmap files introduced (prior commit): 5-phase integration plan + technical implementation details                                                              | `docs/roadmap/context-mode-integration.md`, `docs/roadmap/context-mode-details.md`                |
| 3   | ADR-0029 second-pass audit applied: 12 items (privacy + credential redaction in Decision Drivers, AdGuard `allowed_clients` + `auth_attempts` + `block_auth_min` hardening, 17 captured event categories, monthly version review, embedded `docker/adguard/README.md`) | `docs/adr/0029/0029-context-mode-mcp-sandbox.md`, `docs/roadmap/context-mode-details.md`, `docs/roadmap/context-mode-integration.md` |
| 4   | ADR-0029 ConformanceChecklist: "Env knobs minimalism" added — `.env.context-mode.example` (12 knobs), gitignored `.env.context-mode`, safe defaults (`CONTEXT_MODE_FETCH_STRICT=1`), `${VAR:-default}` interpolation, PR justification required when lowering strict mode | `docs/adr/0029/0029-context-mode-mcp-sandbox.md`                                                  |
| 5   | Roadmap details: "Configurable parameters (env knobs)" section added (3 tables: upstream env vars / compose-level knobs / what is NOT a knob; full `.env.context-mode.example` content; compose snippet updated with `env_file` + `${VAR:-default}` across mem/pids/cpus/tmpfs/strict/nudge/ports; Safety note on `CONTEXT_MODE_FETCH_STRICT` as security boundary); `.gitignore` delta extended with `.env.context-mode` | `docs/roadmap/context-mode-details.md`                                                            |
| 6   | Roadmap integration: Phase 1 scope expanded — rows 1.3b (`.env.context-mode.example` with 12 tunables, committed) and 1.3c (gitignore entry for `.env.context-mode`); file registry updated                                | `docs/roadmap/context-mode-integration.md`                                                        |
| 7   | `.sln` sync: added `0029` ADR solution folder (`adr` tree, items: main file + README router) and the two `context-mode-*.md` roadmap files under existing `roadmap` solution folder; `NestedProjects` updated | `ECommerceApp.sln`                                                                                |
| 8   | Current-state summary table: ADR count 26 → 29 (drift fix; ADR-0027/0028 from RAG work were also previously under-counted)                                       | `.github/COPILOT-SETUP-CHANGELOG.md`                                                              |

**Pre-existing drift flagged (not in this commit)**: ADR-0027 and ADR-0028 are not in `.sln` either. Suggest a separate `.sln` backfill close-out.

### Session 23 — RAG multilingual expansion + maintenance skills (2026-05-19)

| #   | Change                                                                                                                                                           | Files affected                                                                                    |
| --- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| 1   | ADR-0027 §10 added: multilingual query expansion, repeat=3 rationale, benchmark table, newbie guide. Implementation status + conformance checklist updated        | `docs/adr/0027/0027-rag-pipeline-design.md`                                                      |
| 2   | `SETUP-GUIDE.md` multilingual section added: PL/DE example queries, expansion explained, how to add glossary entries                                              | `docs/rag/SETUP-GUIDE.md`                                                                         |
| 3   | Both multilingual glossaries updated: new concept group “Entity Identity & Domain Primitives” (`bezeichner`, `kennung`, `entitäts`, `identyfikator`, `encji`)    | `tools/rag/multilingual-glossary.yaml`, `tools/rag-dotnet/multilingual-glossary.yaml`            |
| 4   | `agent-decisions.md` entry added: @dataclass dropout, _glossary missing after \__new\__, .NET MCP build lock, repeat=3 rationale                                  | `.github/context/agent-decisions.md`                                                              |
| 5   | 4 new RAG maintenance skills created: `diagnose-rag`, `tune-rag-weights`, `expand-rag-glossary`, `generate-rag-rules`                                             | `.github/skills/diagnose-rag/SKILL.md` _(new)_, `tune-rag-weights` _(new)_, `expand-rag-glossary` _(new)_, `generate-rag-rules` _(new)_ |
| 6   | `future-skills.md` updated: date, split Implemented into two groups, 5 RAG skills added to table                                                                  | `.github/context/future-skills.md`                                                                |
| 7   | Changelog current state: skills 11→16, prompts 5→6, prompts inventory: `rag-sync.prompt.md` registered; skills inventory updated to 16 with Session 23 rows   | `.github/COPILOT-SETUP-CHANGELOG.md`                                                              |
| 8   | `docs-index.instructions.md` RAG skills routing table added; `rag.instructions.md` skill decision table + re-index quick-ref added                               | `.github/instructions/docs-index.instructions.md`, `.github/instructions/rag.instructions.md`    |
| 9   | `.sln` sync: 5 RAG maintenance skill solution folders added (`diagnose-rag`, `tune-rag-weights`, `expand-rag-glossary`, `generate-rag-rules`, `generate-eval-questions`) | `ECommerceApp.sln`                                                                          |
| 10  | `rag.instructions.md` + `docs-index.instructions.md`: fixed stale server name — was `ecommerceapp-rag`, now correctly differentiates VS Code (`ecommerceapp-rag-python`/`ecommerceapp-rag-dotnet`) from GitHub.com (`ecommerceapp-rag`) | `.github/instructions/rag.instructions.md`, `.github/instructions/docs-index.instructions.md` |
| 11  | `.vscode/mcp.json`: rewrote top comment block — added explicit "HOW TO SWITCH" guide (Python local / .NET local / Docker variants; one-line Qdrant + ingest commands) | `.vscode/mcp.json`                                                                            |

---

### Session 22 — Docs: TUS V2 complete + close-out sync (2026-05-10)

| #   | Change                                                                                                                                                                                         | Files affected                       |
| --- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------ |
| 1   | `chunked-upload.md` — V2 TUS section rewritten: tusdotnet 2.4.0, `CompleteUpload` bridge, 13 integration tests, cache layer table                                                              | `docs/roadmap/chunked-upload.md`     |
| 2   | `roadmap/README.md` — chunked-upload row updated to "✅ V2 TUS complete (2026-05-10)"                                                                                                          | `docs/roadmap/README.md`             |
| 3   | `project-state.md` — last-updated set to 2026-05-10; TUS Phase 1+2 + hybrid cache layer (`IOutputCache` + `IMemoryCache`, 5 services, `CacheOptions`) summary added                            | `.github/context/project-state.md`   |
| 4   | `agent-decisions.md` — new entry: stash-before-commit correction (2026-05-10)                                                                                                                  | `.github/context/agent-decisions.md` |
| 5   | **Gap fix (Workflow 11)**: `flow-analysis.prompt.md` registered — file existed on disk, referenced in `copilot-instructions.md` §11 and `docs-index`, but missing from CHANGELOG count (4 → 5) | `.github/COPILOT-SETUP-CHANGELOG.md` |

---

### Session 21 — Full audit & metrics refresh (2026-04-26)

| #   | Change                                                                                     | Files affected                       |
| --- | ------------------------------------------------------------------------------------------ | ------------------------------------ |
| 1   | Added `docs\rag\README.md` to `docs` solution folder in `.sln` (file existed, was missing) | `ECommerceApp.sln`                   |
| 2   | Updated repo-index.md At a Glance: CS ~1146→~1155, CSHTML 125→127, tests 132→135 (94+41)   | `.github/context/repo-index.md`      |
| 3   | Updated changelog "Current state summary" test count: 132 → 135 (94 unit + 41 integration) | `.github/COPILOT-SETUP-CHANGELOG.md` |

### Session 20 — Gap fixes, Service vs CQRS guidance, skill option proposals (2026-04-26)

| #   | Change                                                                                                                                                                                        | Files affected                                                                                                                                                           |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1   | `.sln` instructions solution folder: added 4 missing files — `copilot-config-sync`, `bc-adr-map`, `doc-suggestions`, `agent-memory` (gap 1 from gap analysis)                                 | `ECommerceApp.sln`                                                                                                                                                       |
| 2   | `COPILOT-SETUP-CHANGELOG.md` count mismatch fixed: "15" → "16" in current state summary; "(14 files)" → "(16 files)" in inventory header (gap 2)                                              | `.github/COPILOT-SETUP-CHANGELOG.md`                                                                                                                                     |
| 3   | `future-skills.md` updated: date 2026-03-15 → 2026-04-26; "Planned" table: Status column added; 3 skills marked 🟡 options proposed; multi-option rule noted                                  | `.github/context/future-skills.md`                                                                                                                                       |
| 4   | `docs-index.instructions.md` agents table: `@code-reviewer` row updated to include "Standalone only" label (gap 5)                                                                            | `.github/instructions/docs-index.instructions.md`                                                                                                                        |
| 5   | Gap 4 verified as false positive: `AGENT-PIPELINE.md` flow diagram correctly shows `@code-reviewer` embedded inside `@implementer`; no separate pipeline step                                 | _(no change needed)_                                                                                                                                                     |
| 6   | 3 new skills created: `create-cqrs-handler` (Option B — command with result), `create-dto-viewmodel` (Option B — ToDto() extension), `create-message-contract` (Option A — event record only) | `.github/skills/create-cqrs-handler/SKILL.md` _(new)_, `.github/skills/create-dto-viewmodel/SKILL.md` _(new)_, `.github/skills/create-message-contract/SKILL.md` _(new)_ |
| 7   | `docs-index.instructions.md` Skills table: 3 new skill rows added                                                                                                                             | `.github/instructions/docs-index.instructions.md`                                                                                                                        |
| 8   | `ECommerceApp.sln` skills folder: 3 new skill solution items added                                                                                                                            | `ECommerceApp.sln`                                                                                                                                                       |
| 9   | `future-skills.md` Planned table: 3 skills updated from 🟡 options proposed → ✅ implemented; moved to Implemented table                                                                      | `.github/context/future-skills.md`                                                                                                                                       |

### Session 19 — Pipeline hardening, context budget, BC→ADR map (2026-04-26)

| #   | Change                                                                                                                                                                                                                                        | Files affected                                                                                                  |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| 1   | `@implementer` rewritten: embedded 3-probe verification loop (build + unit + integration, 3 max) + embedded inline code review (anti-patterns, BC boundaries)                                                                                 | `.github/agents/implementer.md`                                                                                 |
| 2   | `AGENT-PIPELINE.md` updated: flow diagram, HITL table, max-iter table — `@verifier`/`@code-reviewer` now embedded inside `@implementer`, standalone-only                                                                                      | `.github/AGENT-PIPELINE.md`                                                                                     |
| 3   | `verifier.md` and `code-reviewer.md` YAML descriptions updated: marked **Standalone only** — not invoked during pipeline runs                                                                                                                 | `.github/agents/verifier.md`, `.github/agents/code-reviewer.md`                                                 |
| 4   | `pre-edit.instructions.md`: added mandatory "Post-edit — invoke @copilot-setup-maintainer" section with workflow routing table                                                                                                                | `.github/instructions/pre-edit.instructions.md`                                                                 |
| 5   | `copilot-instructions.md` trimmed from ~4 270 → ~3 670 chars; BC→ADR inline map removed (moved to dedicated file); duplicate §8 heading fixed; §7–§10 renumbered                                                                              | `.github/copilot-instructions.md`                                                                               |
| 6   | `bc-adr-map.instructions.md` created: 680-char BC→ADR quick map, `applyTo: **/*.cs, **/*.csproj, **/*.cshtml` — auto-loads on every code edit                                                                                                 | `.github/instructions/bc-adr-map.instructions.md` _(new)_                                                       |
| 7   | `docs-index.instructions.md` scope narrowed: `applyTo: **` → `.github/**, docs/**` (saves ~20K chars from context on C# edits); bc-adr-map row added; @verifier label corrected                                                               | `.github/instructions/docs-index.instructions.md`                                                               |
| 8   | Fixed RAG commit corruption: 3 table rows that were collapsed into a pipe-string in `docs-index` restored as proper markdown rows                                                                                                             | `.github/instructions/docs-index.instructions.md`                                                               |
| 9   | Restored over-trimmed "Architecture suggestion rule" line in `copilot-instructions.md` (suffix "for ADR, BC map, roadmap, or project-state updates" was lost)                                                                                 | `.github/copilot-instructions.md`                                                                               |
| 10  | `pre-edit.instructions.md` split: mandatory checklist kept (steps 0–8 + post-edit rules + @copilot-setup-maintainer gate); doc-suggestion triggers moved to new `doc-suggestions.instructions.md`. `pre-edit` drops from 7 708 → ~2 600 chars | `.github/instructions/pre-edit.instructions.md`, `.github/instructions/doc-suggestions.instructions.md` _(new)_ |
| 11  | `doc-suggestions.instructions.md` registered in `docs-index` and `COPILOT-SETUP-CHANGELOG.md` inventory                                                                                                                                       | `.github/instructions/docs-index.instructions.md`, `.github/COPILOT-SETUP-CHANGELOG.md`                         |
| 12  | `copilot-instructions.md` §3: Multi-option rule added (propose 2–5 approaches before implementing architectural decisions)                                                                                                                    | `.github/copilot-instructions.md`                                                                               |
| 13  | `copilot-instructions.md` §4: Sync rule added (mandatory @copilot-setup-maintainer after any `.github/` or `docs/` change)                                                                                                                    | `.github/copilot-instructions.md`                                                                               |
| 14  | `agent-memory.instructions.md` created (`applyTo: **`): auto-loads the "read agent-decisions.md before any non-trivial task" rule                                                                                                             | `.github/instructions/agent-memory.instructions.md` _(new)_, `.github/instructions/docs-index.instructions.md`  |
| 15  | `docs/rag/README.md`: "Planned improvements" section added — 7 items: CI re-index, multilingual embedder, persistent Qdrant, file-watcher, eval expansion, synthesis wiring, chunk explain flag                                               | `docs/rag/README.md`                                                                                            |

### Session 18 — Local RAG MVP (2026-04-22)

| #   | Change                                                                                                                                                              | Files affected                                                                                  |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| 1   | RAG pipeline core: `rag-config.yaml` (all knobs), `common.py` (helpers + weight resolver), `chunker.py` (heading-aware + breadcrumb + overlap), `ingest.py`, `query.py` | `tools/rag/rag-config.yaml`, `common.py`, `chunker.py`, `ingest.py`, `query.py`, `requirements.txt` |
| 2   | Eval suite: 20 anchor questions + recall@k reporter (acceptance bar: recall@8 ≥ 80 %)                                                                               | `tools/rag/eval/questions.json`, `tools/rag/eval/eval.py`                                       |
| 3   | MCP server with 3 tools: `query_docs`, `get_adr_history`, `list_adrs`; registered in VS Code via `mcp.json`                                                         | `tools/rag/mcp_server.py`, `.github/copilot/mcp.json`                                           |
| 4   | `rag.instructions.md` — routing precedence (docs-index FIRST, RAG as fallback), output discipline, refresh policy                                                   | `.github/instructions/rag.instructions.md`, `.github/instructions/docs-index.instructions.md`   |
| 5   | `docs/rag/README.md` — setup TL;DR, what's indexed, chunking, ranking weights, CLI usage, eval, MCP tools, troubleshooting                                          | `docs/rag/README.md`                                                                            |
| 6   | Presentation slides on Decisions (ADR + agent-decisions.md) in Marp format; workflow/hierarchy section added                                                        | `docs/presentations/copilot-decisions-slides.md`                                                |
| 7   | `.gitignore` updated: `tools/rag/.cache/` and `tools/rag/.venv/` excluded                                                                                           | `.gitignore`                                                                                    |
| 8   | `.sln` updated: `mcp.json` added to Copilot folder; `rag.instructions.md` added to instructions items                                                               | `ECommerceApp.sln`                                                                              |

### Session 17 — Multi-agent pipeline + memory + CI (2026-04-21)

| #   | Change                                                                                                                                                                                         | Files affected                                                                          |
| --- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| 1   | Added `agent-decisions.md` append-only log of in-session agent corrections; promotion path to ADR/anti-patterns                                                                                | `.github/context/agent-decisions.md`                                                    |
| 2   | Pre-edit gate: added Step 0 (read agent-decisions) + post-edit append rule                                                                                                                     | `.github/instructions/pre-edit.instructions.md`                                         |
| 3   | Added context-files row + agent memory rule for `agent-decisions.md`                                                                                                                           | `.github/instructions/docs-index.instructions.md`, `.github/copilot-instructions.md`    |
| 4   | Added `.github/workflows/dotnet-ci.yml` (build + unit + integration + arch via UnitTests; manual trigger only, push/PR commented)                                                              | `.github/workflows/dotnet-ci.yml`                                                       |
| 5   | New `/refactor.prompt.md` for structural / readability refactors with HITL after planning step                                                                                                 | `.github/prompts/refactor.prompt.md`, `.github/instructions/docs-index.instructions.md` |
| 6   | Added 4 pipeline agents: `planner`, `implementer`, `verifier`, `pr-commit` with `max-iterations:` and explicit HITL checkpoints                                                                | `.github/agents/planner.md`, `implementer.md`, `verifier.md`, `pr-commit.md`            |
| 7   | Pipeline orchestration spec with full HITL/max-iter table and failure-mode matrix                                                                                                              | `.github/AGENT-PIPELINE.md`                                                             |
| 8   | Added `max-iterations:` and explicit HITL checkpoints to existing agents (`adr-generator` HITL before write, `bc-switch` HITL after Step 1 + before Step 6 delete, `code-reviewer` max-iter 3) | `.github/agents/adr-generator.md`, `bc-switch.md`, `code-reviewer.md`                   |
| 9   | code-reviewer: added agent-decisions awareness, pipeline awareness section, refactor detection section                                                                                         | `.github/agents/code-reviewer.md`                                                       |
| 10  | copilot-setup-maintainer: added Workflow 12 (pipeline orchestration sync) and ownership of `AGENT-PIPELINE.md`                                                                                 | `.github/agents/copilot-setup-maintainer.md`                                            |
| 11  | docs-index: added pipeline agents (planner/implementer/verifier/pr-commit) to Agents table + AGENT-PIPELINE link                                                                               | `.github/instructions/docs-index.instructions.md`                                       |

### Session 16 — Docs router + ADR folder sync (2026-04-19)

| #   | Change                                                                                                                                                                                                                       | Files affected                                                                                                |
| --- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| 1   | Added `docs/README.md` as the short human-facing router for the `docs/` tree                                                                                                                                                 | `docs/README.md`                                                                                              |
| 2   | Updated docs-index to treat ADR folder `README.md` files as the Copilot entry points and added a root docs section                                                                                                           | `.github/instructions/docs-index.instructions.md`                                                             |
| 3   | Strengthened docs auto-sync rules so meaningful docs changes trigger `.github` environment refresh guidance and an explicit feed-forward loop                                                                                | `.github/instructions/copilot-config-sync.instructions.md`, `.github/instructions/pre-edit.instructions.md`   |
| 4   | Updated Copilot prompts/instructions/agents for folderized ADR paths, new ADR output structure, and central feed-forward wording                                                                                             | `.github/copilot-instructions.md`, `.github/prompts/*.md`, `.github/instructions/*.md`, `.github/agents/*.md` |
| 5   | Updated `project-state.md` ADR links to folder routers and fixed docs-relative paths                                                                                                                                         | `.github/context/project-state.md`                                                                            |
| 6   | Synced solution docs items: added `docs\README.md`; switched ADR solution items to per-ADR solution folders with nested folder-local files (`amendments`, `example-implementation`, checklist, migration plan where present) | `ECommerceApp.sln`                                                                                            |
| 7   | Tightened maintainer/auto-sync flow so nested ADR solution structure and an end-of-task repo sync close-out check are explicit                                                                                               | `.github/agents/copilot-setup-maintainer.md`, `.github/instructions/copilot-config-sync.instructions.md`      |

### Session 15 — Saga implementation sync (2026-04-15)

| #   | Change                                                                                                                                                    | Files affected                                                              |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------- |
| 1   | `PaymentStatus.Cancelled` added to enum (commit 030797a)                                                                                                  | `ECommerceApp.Domain/Sales/Payments/PaymentStatus.cs`                       |
| 2   | `Payment.Cancel()` domain method added — throws `DomainException` if status is not `Pending`                                                              | `ECommerceApp.Domain/Sales/Payments/Payment.cs`                             |
| 3   | `PaymentOperationResult.AlreadyCancelled` added                                                                                                           | `ECommerceApp.Application/Sales/Payments/Results/PaymentOperationResult.cs` |
| 4   | `PaymentService.ConfirmAsync` — guard added: returns `AlreadyCancelled` when payment status is `Cancelled`                                                | `ECommerceApp.Application/Sales/Payments/Services/PaymentService.cs`        |
| 5   | `PaymentsController.ConfirmPayment` — Polish error message added for `AlreadyCancelled` case                                                              | `ECommerceApp.Web/Areas/Sales/Controllers/PaymentsController.cs`            |
| 6   | `PaymentAggregateTests` — 3 new `Cancel` tests; `PaymentStatus.Cancelled` added as inline data to 2 existing non-Pending theories                         | `ECommerceApp.UnitTests/Sales/Payments/PaymentAggregateTests.cs`            |
| 7   | `OrderPlacementFailedFanOutTests.cs` (NEW) — 6 cross-BC integration tests: Payments compensation (×2), Inventory compensation (×2), combined fan-out (×2) | `ECommerceApp.IntegrationTests/CrossBC/OrderPlacementFailedFanOutTests.cs`  |
| 8   | Updated repo-index test counts: 131→132 total (integration 39→40)                                                                                         | `.github/context/repo-index.md`                                             |
| 9   | Updated project-state.md: Saga Option A (OrderPlacementFailed compensation — Payments + Inventory + Presale) marked complete                              | `.github/context/project-state.md`                                          |

### Session 14 — Full audit & ADR-0026 sync (2026-04-15)

| #   | Change                                                                                                                                   | Files affected                                             |
| --- | ---------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------- |
| 1   | Added ADR-0026 (Order lifecycle saga) row to ADR table in docs-index                                                                     | `.github/instructions/docs-index.instructions.md`          |
| 2   | Added `saga-pattern.md` row to roadmap table in docs-index (was missing — file existed on disk and in .sln)                              | `.github/instructions/docs-index.instructions.md`          |
| 3   | Incremented ADR count §2: 25 → 26                                                                                                        | `.github/copilot-instructions.md`                          |
| 4   | Added `docs\adr\0026-order-lifecycle-saga.md` to `adr` solution folder                                                                   | `ECommerceApp.sln`                                         |
| 5   | Extended `copilot-config-sync.instructions.md` `applyTo:` to `.github/**, docs/**`; added ADR/roadmap trigger sections for docs/ changes | `.github/instructions/copilot-config-sync.instructions.md` |
| 6   | Updated `repo-index.md` At a Glance: CS ~1054→~1146, CSHTML 103→125, ADRs 25→26, tests 116→131 (92+39)                                   | `.github/context/repo-index.md`                            |

### Session 13 — Full audit & sync (2026-04-09)

| #   | Change                                                                                                                                                                | Files affected                                                        |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| 1   | Added `storefront-offers.md` and `chunked-upload.md` rows to roadmap table in docs-index                                                                              | `.github/instructions/docs-index.instructions.md`                     |
| 2   | Removed stale `[DD-002]` link from `copilot-instructions.md` §9 (DD-002 is resolved)                                                                                  | `.github/copilot-instructions.md`                                     |
| 3   | Fixed `.sln` context folder: `anti-patterns.context.md` → `anti-patterns-critical.context.md`                                                                         | `ECommerceApp.sln`                                                    |
| 4   | Added `storefront-offers.md` and `chunked-upload.md` to `roadmap` solution folder                                                                                     | `ECommerceApp.sln`                                                    |
| 5   | Added `auth.http` to `HttpScenarios` solution folder (was missing; file existed on disk)                                                                              | `ECommerceApp.sln`                                                    |
| 6   | Updated `repo-index.md` At a Glance: CS ~1143→~1054, CSHTML 176→103, JS 11→12, HTTP 9→10, tests 149→116 (79+37)                                                       | `.github/context/repo-index.md`                                       |
| 7   | Updated `repo-index.md` Solution Projects table: all 7 project file counts corrected                                                                                  | `.github/context/repo-index.md`                                       |
| 8   | Updated `project-state.md` + `known-issues.md`: BrandService/ImageService legacy table corrected; KI-007/KI-009/DD-002 moved to Resolved; KI-005/KI-006 dupes removed | `.github/context/project-state.md`, `.github/context/known-issues.md` |

### Session 12 — Project status sync (2026-03-26)

| #   | Change                                                                                                                                                       | Files affected                       |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------ |
| 1   | Fixed `copilot-instructions.md` §2 instruction file count: 11 → 12 (copilot-config-sync was not counted)                                                     | `.github/copilot-instructions.md`    |
| 2   | Fixed `project-state.md` date: `2026-05-27` (typo) → `2026-03-26`; added IAM + refresh token active work row                                                 | `.github/context/project-state.md`   |
| 3   | Updated `iam-refresh-token.md`: status Planned → 🟡 In progress; Steps 1–4 marked done (entity, infra, service, unit tests)                                  | `docs/roadmap/iam-refresh-token.md`  |
| 4   | Updated `iam-atomic-switch.md`: status expanded; added refresh token + Area controller rows to "already done" table                                          | `docs/roadmap/iam-atomic-switch.md`  |
| 5   | Fixed changelog Session 5 date: `2026-06-27` (typo) → `2026-03-17`                                                                                           | `.github/COPILOT-SETUP-CHANGELOG.md` |
| 6   | Updated `repo-index.md`: date, Razor views 153→176, test counts, IAM section (Area controller + RefreshToken), Jobs/TimeManagement section (Area controller) | `.github/context/repo-index.md`      |

### Session 11 — Setup audit, anti-patterns rename, trigger phrases (2026-03-28)

| #   | Change                                                                                                        | Files affected                                      |
| --- | ------------------------------------------------------------------------------------------------------------- | --------------------------------------------------- |
| 1   | Renamed `anti-patterns.context.md` → `anti-patterns-critical.context.md` (both guides prescribe split naming) | `.github/context/anti-patterns-critical.context.md` |
| 2   | Updated all references to old filename (code-reviewer, docs-index, copilot-config-sync, maintainer)           | 4 files                                             |
| 3   | Added YAML frontmatter + trigger phrases to `copilot-setup-maintainer.md`                                     | `.github/agents/copilot-setup-maintainer.md`        |
| 4   | Added trigger phrases to all 4 agent YAML descriptions                                                        | `.github/agents/*.md`                               |
| 5   | Fixed instruction file count 11→12 in changelog (copilot-config-sync was missing)                             | `.github/COPILOT-SETUP-CHANGELOG.md`                |
| 6   | Added `copilot-config-sync.instructions.md` to instruction file inventory                                     | `.github/COPILOT-SETUP-CHANGELOG.md`                |

### Session 10 — IAM refresh token roadmap + docs-index sync (2026-03-27)

| #   | Change                                                                                                                                             | Files affected                                    |
| --- | -------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------- |
| 1   | Created `docs/roadmap/iam-refresh-token.md` — 8-step plan, design decisions settled (DB storage, Jti binding, rotation-on-use, deferred SessionId) | `docs/roadmap/iam-refresh-token.md`               |
| 2   | Added `iam-refresh-token.md` to active roadmaps table in `README.md`                                                                               | `docs/roadmap/README.md`                          |
| 3   | Removed stale "IAM refresh token" entry from Deferred section (now tracked as active roadmap)                                                      | `docs/roadmap/README.md`                          |
| 4   | Removed stale "Bootstrap 5 upgrade" entry from Deferred section (completed — ADR-0023)                                                             | `docs/roadmap/README.md`                          |
| 5   | Added `iam-refresh-token.md` row to docs-index roadmap table                                                                                       | `.github/instructions/docs-index.instructions.md` |
| 6   | Updated changelog with Session 10 entry                                                                                                            | `.github/COPILOT-SETUP-CHANGELOG.md`              |

### Session 9 — HTTP scenarios refresh + .sln sync (2026-03-25)

| #   | Change                                                                                                    | Files affected                                        |
| --- | --------------------------------------------------------------------------------------------------------- | ----------------------------------------------------- |
| 1   | Deleted 9 stale `*-v2.http` scenario files (routes were `api/v2/...`)                                     | `ECommerceApp.API/HttpScenarios/`                     |
| 2   | Created `payments.http` — `GET api/payments/{paymentId:guid}` (own-scoped by Guid token)                  | `ECommerceApp.API/HttpScenarios/payments.http`        |
| 3   | Created `presale.http` — `api/cart`, `api/checkout` (removed price-changes step; confirm returns orderId) | `ECommerceApp.API/HttpScenarios/presale.http`         |
| 4   | Created `sales-orders.http` — `GET api/orders/{id}` with paymentUrl in response                           | `ECommerceApp.API/HttpScenarios/sales-orders.http`    |
| 5   | Created `catalog.http` — `api/items` published-only GET list + GET {id}                                   | `ECommerceApp.API/HttpScenarios/catalog.http`         |
| 6   | Created `account-profile.http` — `api/customers` + `api/addresses` (POST/PUT/DELETE)                      | `ECommerceApp.API/HttpScenarios/account-profile.http` |
| 7   | Created `refunds.http` — `api/refunds` GET {id} + POST (removed admin approve/reject)                     | `ECommerceApp.API/HttpScenarios/refunds.http`         |
| 8   | Created `currencies.http` — kept `api/v2/currencies` (admin controller, not part of BC switch)            | `ECommerceApp.API/HttpScenarios/currencies.http`      |
| 9   | Created `inventory.http` — kept `api/v2/inventory` (admin controller, not part of BC switch)              | `ECommerceApp.API/HttpScenarios/inventory.http`       |
| 10  | Created `jobs.http` — kept `api/v2/jobs` (admin controller, not part of BC switch)                        | `ECommerceApp.API/HttpScenarios/jobs.http`            |
| 11  | Added `docs\reports\web-ui-views-report.md` to `reports` solution folder                                  | `ECommerceApp.sln`                                    |
| 12  | Added `HttpScenarios` solution folder with all 9 `.http` files as solution items                          | `ECommerceApp.sln`                                    |

### Session 8 — Full audit & repo-index sync (2026-03-23)

| #   | Change                                                                                    | Files affected                       |
| --- | ----------------------------------------------------------------------------------------- | ------------------------------------ |
| 1   | Added `docs\reference\endpoint-map.md` (and new `reference` solution folder) to solution  | `ECommerceApp.sln`                   |
| 2   | Updated repo-index At a Glance: Test files 156→161 (integration 60→65)                    | `.github/context/repo-index.md`      |
| 3   | Updated repo-index Solution Projects: UnitTests 76→96 files, IntegrationTests 42→65 files | `.github/context/repo-index.md`      |
| 4   | Updated repo-index section headings: UnitTests 76→96, IntegrationTests 42→65              | `.github/context/repo-index.md`      |
| 5   | Updated changelog with Session 8 entry                                                    | `.github/COPILOT-SETUP-CHANGELOG.md` |

### Session 7 — Full audit & trim (2026-03-22)

| #   | Change                                                                                                            | Files affected                       |
| --- | ----------------------------------------------------------------------------------------------------------------- | ------------------------------------ |
| 1   | Trimmed `copilot-instructions.md` from 5,506 → 3,724 chars: condensed §2 bullets, §4 BC rule, §6 arch rule, §7–§9 | `.github/copilot-instructions.md`    |
| 2   | Fixed ADR count 23 → 25 in §2 of `copilot-instructions.md`                                                        | `.github/copilot-instructions.md`    |
| 3   | Added ADR-0025 to `adr` solution folder                                                                           | `ECommerceApp.sln`                   |
| 4   | Updated repo-index At a Glance: ADRs 24→25, Razor views 134→153, JS modules 10→11                                 | `.github/context/repo-index.md`      |
| 5   | Updated changelog current state summary and added Session 7 entry                                                 | `.github/COPILOT-SETUP-CHANGELOG.md` |

### Session 6 — ADR-0024 + full audit (2026-03-22)

| #   | Change                                                                                 | Files affected                                      |
| --- | -------------------------------------------------------------------------------------- | --------------------------------------------------- |
| 1   | Created ADR-0024 (controller routing strategy: Areas for Web, in-place swap for API)   | `docs/adr/0024/0024-controller-routing-strategy.md` |
| 2   | Added ADR-0024 row to docs-index ADR table                                             | `.github/instructions/docs-index.instructions.md`   |
| 3   | Added ADR-0024 routing strategy note to roadmap README                                 | `docs/roadmap/README.md`                            |
| 4   | Updated `orders-atomic-switch.md` Steps 3–4 to Area-based approach + ADR-0024 ref      | `docs/roadmap/orders-atomic-switch.md`              |
| 5   | Updated `payments-atomic-switch.md` Step 3 to Area-based approach + ADR-0024 ref       | `docs/roadmap/payments-atomic-switch.md`            |
| 6   | Added routing strategy as key architectural invariant                                  | `.github/context/project-state.md`                  |
| 7   | ADR-0024 added to `ECommerceApp.sln` `adr` solution folder (auto by VS on file create) | `ECommerceApp.sln`                                  |
| 8   | Full audit (Workflow 6 + Workflow 8): repo-index At a Glance metrics updated           | `.github/context/repo-index.md`                     |
| 9   | Updated changelog counts and added Session 6 entry                                     | `COPILOT-SETUP-CHANGELOG.md`                        |

### Session 5 — Full audit & .sln sync (2026-03-17)

| #   | Change                                                                         | Files affected                                    |
| --- | ------------------------------------------------------------------------------ | ------------------------------------------------- |
| 1   | Added ADR-0022 (Navbar) and ADR-0023 (Bootstrap 5) to sln adr folder           | `ECommerceApp.sln`                                |
| 2   | Added `code-reviewer.md` to sln agents folder                                  | `ECommerceApp.sln`                                |
| 3   | Added `anti-patterns.context.md` to sln context folder                         | `ECommerceApp.sln`                                |
| 4   | Created `reports` solution folder; added `cross-bc-integration-test-report.md` | `ECommerceApp.sln`                                |
| 5   | Added `docs/reports/` section to docs-index                                    | `.github/instructions/docs-index.instructions.md` |
| 6   | Updated changelog with Session 5                                               | `COPILOT-SETUP-CHANGELOG.md`                      |

### Session 4 — Drift audit & sync (2026-03-17)

| #   | Change                                          | Files affected                                    |
| --- | ----------------------------------------------- | ------------------------------------------------- |
| 1   | Fixed ADR count 21 → 23 in copilot-instructions | `.github/copilot-instructions.md`                 |
| 2   | Added missing ADR-0022 row to docs-index        | `.github/instructions/docs-index.instructions.md` |
| 3   | Updated changelog with Session 4                | `COPILOT-SETUP-CHANGELOG.md`                      |

### Session 3 — Code reviewer & anti-patterns (2026-03-17)

| #   | Change                                               | Files affected                                    |
| --- | ---------------------------------------------------- | ------------------------------------------------- |
| 1   | Created consolidated anti-patterns context file      | `.github/context/anti-patterns.context.md`        |
| 2   | Created code-reviewer agent                          | `.github/agents/code-reviewer.md`                 |
| 3   | Added repo-index verification workflow to maintainer | `.github/agents/copilot-setup-maintainer.md`      |
| 4   | Updated docs-index with new agent and context file   | `.github/instructions/docs-index.instructions.md` |
| 5   | Updated agent count in copilot-instructions          | `.github/copilot-instructions.md`                 |
| 6   | Updated changelog counts and inventory               | `COPILOT-SETUP-CHANGELOG.md`                      |

### Session 2 — Skills & codebase index (2026-03-15)

| #   | Change                                          | Files affected                     |
| --- | ----------------------------------------------- | ---------------------------------- |
| 1   | Created full codebase index                     | `.github/context/repo-index.md`    |
| 2   | Created skills roadmap                          | `.github/context/future-skills.md` |
| 3   | Created 8 SKILL.md files                        | `.github/skills/` (8 folders)      |
| 4   | Added skills section to docs-index              | `docs-index.instructions.md`       |
| 5   | Added skills listing to copilot-instructions    | `copilot-instructions.md` §2       |
| 6   | Updated changelog to include current state      | `COPILOT-SETUP-CHANGELOG.md`       |
| 7   | Added changelog maintenance to setup-maintainer | `copilot-setup-maintainer.md`      |

### Session 1 — Initial setup overhaul

| #   | Change                                                                   | Files affected                                                                         |
| --- | ------------------------------------------------------------------------ | -------------------------------------------------------------------------------------- |
| 1   | Renamed 7 instruction files to `.instructions.md`                        | `dotnet`, `efcore`, `frontend`, `razorpages`, `web-api`, `testing`, `migration-policy` |
| 2   | Renamed 3 prompt files to `.prompt.md`                                   | `bc-analysis`, `bc-implementation`, `pr-review`                                        |
| 3   | Trimmed `copilot-instructions.md` from 11,689 → 3,984 chars              | `.github/copilot-instructions.md`                                                      |
| 4   | Created `safety.instructions.md` (extracted from copilot-instructions)   | `.github/instructions/safety.instructions.md`                                          |
| 5   | Created `pre-edit.instructions.md` (extracted from copilot-instructions) | `.github/instructions/pre-edit.instructions.md`                                        |
| 6   | Created `docs-index.instructions.md` (docs lookup table)                 | `.github/instructions/docs-index.instructions.md`                                      |
| 7   | Updated usage hints in prompt files                                      | All 3 `.prompt.md` files                                                               |
| 8   | Fixed all cross-references (20+ broken filenames)                        | Multiple files                                                                         |
| 9   | Created `@copilot-setup-maintainer` agent                                | `.github/agents/copilot-setup-maintainer.md`                                           |

---

## Risks & rollback

- **All changes are additive** — no application code touched.
- **Git rollback**: `git checkout HEAD -- .github/` reverts everything.
- **IDE requirements**: `applyTo:` frontmatter requires VS Code ≥ 1.99 or Visual Studio 17.14+.
