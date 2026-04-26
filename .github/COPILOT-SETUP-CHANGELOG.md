# Copilot Setup — Changelog & Current State

> This file tracks **what changed** in the `.github/` Copilot configuration and serves
> as a snapshot of the current setup. Updated by `@copilot-setup-maintainer` or manually.
>
> Use as a quick reference to see what exists without scanning every file.

---

## Current state summary

| Category                               | Count | Details                                                                                                                            |
| -------------------------------------- | ----- | ---------------------------------------------------------------------------------------------------------------------------------- |
| `copilot-instructions.md`              | 1     | ~3 980 chars (under 4K); Multi-option rule (§3), Sync rule (§4), agent-memory ref (§6) added                                       |
| Instruction files (`.instructions.md`) | 16    | All with `applyTo:` frontmatter; `agent-memory` added; `pre-edit` split into core + `doc-suggestions`; `docs-index` scope narrowed |
| Prompt files (`.prompt.md`)            | 4     | BC analysis, BC implementation, PR review, refactor                                                                                |
| Agent files                            | 8     | adr-generator, bc-switch, code-reviewer, copilot-setup-maintainer, planner, implementer, verifier, pr-commit                       |
| Skills (`SKILL.md`)                    | 11    | Scaffolding templates for common artifacts; +3 new: cqrs-handler, dto-viewmodel, message-contract                                  |
| ADRs                                   | 26    | Folderized ADR routers under `docs/adr/<NNNN>/README.md`                                                                           |
| Context files                          | 6     | project-state, known-issues, agent-decisions, repo-index, future-skills, anti-patterns-critical                                    |
| GitHub Actions workflows               | 1     | `dotnet-ci.yml` — manual trigger only (push/PR commented)                                                                          |
| Pipeline orchestration spec            | 1     | `.github/AGENT-PIPELINE.md`; `@verifier`/`@code-reviewer` embedded inside `@implementer`; standalone-only for one-off use          |
| HTTP scenario files                    | 10    | +auth.http (was 9)                                                                                                                 |
| Test files                             | 135   | 94 unit + 41 integration                                                                                                           |

---

## File inventory

### `.github/instructions/` (16 files)

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

### `.github/prompts/` (4 files)

| File                          | Added               |
| ----------------------------- | ------------------- |
| `bc-analysis.prompt.md`       | Session 1 (renamed) |
| `bc-implementation.prompt.md` | Session 1 (renamed) |
| `pr-review.prompt.md`         | Session 1 (renamed) |
| `refactor.prompt.md`          | Session 17 (new)    |

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

### `.github/skills/` (8 skills)

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

### Session 21 — Full audit & metrics refresh (2026-04-26)

| #   | Change                                                                                      | Files affected                           |
| --- | ------------------------------------------------------------------------------------------- | ---------------------------------------- |
| 1   | Added `docs\rag\README.md` to `docs` solution folder in `.sln` (file existed, was missing)  | `ECommerceApp.sln`                        |
| 2   | Updated repo-index.md At a Glance: CS ~1146→~1155, CSHTML 125→127, tests 132→135 (94+41)   | `.github/context/repo-index.md`           |
| 3   | Updated changelog "Current state summary" test count: 132 → 135 (94 unit + 41 integration) | `.github/COPILOT-SETUP-CHANGELOG.md`      |

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
| 1   | RAG pipeline core: `config.yaml` (all knobs), `common.py` (helpers + weight resolver), `chunker.py` (heading-aware + breadcrumb + overlap), `ingest.py`, `query.py` | `tools/rag/config.yaml`, `common.py`, `chunker.py`, `ingest.py`, `query.py`, `requirements.txt` |
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
