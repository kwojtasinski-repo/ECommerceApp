# Copilot Setup — Changelog & Current State

> This file tracks **what changed** in the `.github/` Copilot configuration and serves
> as a snapshot of the current setup. Updated by `@copilot-setup-maintainer` or manually.
>
> Use as a quick reference to see what exists without scanning every file.


## Session 71 — explicit RAG -> context-mode -> classic fallback workflow (2026-06-14)

Added an explicit default workflow rule to the canonical MCP routing file and the repo-level Copilot instructions:

 Top-of-file rules now state the workflow before the rest of the policy text.
- Top-level rules now define the abbreviations: RAG = repo-doc MCP servers, context-mode = sandbox MCP server.
- context-mode isolation is now explicit: it must not invoke RAG or any `mcp__rag*` tool.
- RAG first for documentation / ADR / project knowledge.
- context-mode next for derived results, analysis, math, code generation, and transformations.
- context-mode is explicitly defined as the local sandbox for thinking in code: read local files/snippets, search indexed session data, compute reductions, compare outputs, and generate code fragments before touching files.
- classic tools only when the MCP step fails or is unavailable.
- if RAG is empty/unavailable, stay in MCP and try context-mode on local files/snippets before classic fallback.
- for implementation tasks, context-mode is the first probe even before classic repo reads; `read_file` / `grep_search` come later only if MCP gives no useful signal.

Also added a compact ASCII flow to `docs/roadmap/context-mode-integration.md` so the rollout path is obvious to maintainers.


## Current state summary

| Category                               | Count | Details                                                                                                                            |
| -------------------------------------- | ----- | ---------------------------------------------------------------------------------------------------------------------------------- |
| `copilot-instructions.md`              | 1     | ~8 100 chars; §15 Context budget + Progressive Disclosure (Tier 0-3) + Navigation Map added (Session 62)                           |
| Instruction files (`.instructions.md`) | 18    | All with `applyTo:` frontmatter; +`batched-tasks` (Session 26); `agent-memory` added; `pre-edit` split into core + `doc-suggestions`; `docs-index` scope narrowed |
| Prompt files (`.prompt.md`)            | 9     | +`general.prompt.md` (Session 62); flow-analysis, batched-tasks, mcp-routing-eval, rag-sync, refactor also included                |
| Agent files                            | 10    | +`spec-writer.md` (Session 62); adr-generator, bc-switch, code-reviewer, copilot-setup-maintainer, planner, implementer, verifier, pr-commit, setup-discovery |
| Skills (`SKILL.md`)                    | 36    | +`code-validator`, `mermaid-diagram`, `context-updater` (Session 62); +9 cross-project bootstrap skills (Session 33); +4 RAG maintenance skills (Session 32); +3 context-mode sandbox skills (Session 31); +rag-with-memory (Session 26); +5 RAG skills (Session 23) |
| ADRs                                   | 29    | Folderized ADR routers under `docs/adr/<NNNN>/README.md`; ADR-0027/0028 (RAG pipeline) + ADR-0029 (context-mode sandbox) added    |
| Context files                          | 8     | +`anti-patterns-advisory.context.md` (Session 62); project-state, known-issues, agent-decisions, repo-index, future-skills, anti-patterns-critical, test-stabilization-policy |
| GitHub Actions workflows               | 1     | `dotnet-ci.yml` — manual trigger only (push/PR commented)                                                                          |
| Pipeline orchestration spec            | 1     | `.github/AGENT-PIPELINE.md`; `@verifier`/`@code-reviewer` embedded inside `@implementer`; `@spec-writer` added to domain agents table (Session 62) |
| HTTP scenario files                    | 10    | +auth.http (was 9)                                                                                                                 |
| Test files                             | 135   | 94 unit + 41 integration                                                                                                           |

---

## File inventory

### `.github/instructions/` (18 files)

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
| `mcp-routing.instructions.md`         | `**`                                                          | Session 27 (new — single-source MCP routing and precedence rules)            |
| `rag.instructions.md`                 | `.github/**, docs/**, tools/rag/**, tools/rag-dotnet/**`     | Session 18 (new — RAG routing precedence + output discipline); Session 56 (scope narrowed from `**`) |
| `bc-adr-map.instructions.md`          | `**/*.cs, **/*.csproj, **/*.cshtml`                           | Session 19 (new — BC → ADR quick map for code edits)                        |
| `doc-suggestions.instructions.md`     | `**`                                                          | Session 19 (new — proactive doc suggestion triggers; split from `pre-edit`) |
| `agent-memory.instructions.md`        | `**`                                                          | Session 19 (new — auto-loads `agent-decisions.md` read rule on every task)  |
| `batched-tasks.instructions.md`       | `**`                                                          | Session 26 (new — extracted from `copilot-instructions.md` §14 to keep auto-load while shrinking the root policy file) |

### `.github/prompts/` (8 files)

| File                          | Added                                                                                                                                   |
| ----------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| `bc-analysis.prompt.md`       | Session 1 (renamed)                                                                                                                     |
| `bc-implementation.prompt.md` | Session 1 (renamed)                                                                                                                     |
| `pr-review.prompt.md`         | Session 1 (renamed)                                                                                                                     |
| `refactor.prompt.md`          | Session 17 (new)                                                                                                                        |
| `flow-analysis.prompt.md`     | Session 22 (registered — file existed on disk, referenced in `copilot-instructions.md` §11 and `docs-index`, omitted from prior counts) |
| `batched-tasks.prompt.md`     | Session 26 (registered — structured response format for 3+ actionable items)                                                           |
| `mcp-routing-eval.prompt.md`  | Session 27 (registered — stricter eval-mode format for batched MCP routing checks)                                                     |
| `rag-sync.prompt.md`          | Session 23 (registered — file existed on disk, runs incremental ingest + eval validation + coverage check)                             |

### `.github/agents/` (9 files)

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
| `setup-discovery.md`          | Session 33 (new — read-only bootstrap audit for RAG/context-mode/MCP client setup in new repositories) |

### `.github/skills/` (33 skills)

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
| `ctx-sandbox-bootstrap-verify` | Runtime smoke after `docker compose up context-mode` (8 checks; non-root, 6 hardening flags, ctx-net+DNS, network-monitor, FTS5, ctx_doctor, loopback ports, AdGuard ACL) | Session 31 |
| `ctx-doctor-playbook`     | Map `ctx_doctor` messages + container/runtime errors → cause → fix          | Session 31 |
| `ctx-hardening-audit`     | Programmatic verification of all 22 ADR-0029 Conformance items (pre-merge gate) | Session 28 |

The full current 33-skill set also includes the cross-project/bootstrap and advanced RAG operations skills listed in the Current state summary, including `ctx-bootstrap-network`, `ctx-bootstrap-storage`, `ctx-bootstrap-runtimes`, `setup-rag-new-project`, `setup-context-mode-new-project`, `setup-adguard-policy`, `setup-mcp-clients`, `setup-auto-cache-hook`, `rag-eval-coverage`, `rag-reindex-decision`, `rag-collection-rebuild`, `rag-query-debug`, and `rag-multilang-test`.

### `.github/context/` (7 files)

| File                                | Added                                                |
| ----------------------------------- | ---------------------------------------------------- |
| `project-state.md`                  | Pre-existing                                         |
| `known-issues.md`                   | Pre-existing                                         |
| `agent-decisions.md`                | Session 17 (new — append-only agent corrections log) |
| `repo-index.md`                     | Session 2 (new — full codebase map)                  |
| `future-skills.md`                  | Session 2 (new — skills roadmap)                     |
| `anti-patterns-critical.context.md` | Session 3 (renamed from anti-patterns.context.md)    |
| `test-stabilization-policy.md`      | Session 21 (new — skip/xfail policy and tracking requirements) |

---

## Change log

### Session 69 — RAG config simplification: optional companions, scope dictionary, E2E fixes (2026-06-09)

Faza A+B+C refaktoru RAG pipeline zgodnie z planem z Session 69.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | **companion files optional** — `metadata-rules.yaml` and `queries.yaml` are no longer required companion files in the ingest ZIP. `rag-config.yaml` is now the single required entry point; `metadata_rules.doc_kind_rules` and `named_queries` can be embedded inline in `rag-config.yaml` directly. Missing companion files fall back to inline values; missing named_queries produces a warning, not an error. | `tools/rag/ingest_routes.py` |
| 2 | **scope dictionary for `query_docs_cached`** — Python `_derive_source_label` and `_format_chunks_to_markdown` refactored to accept a generic `scope_filter` + `scope_key` pair. `bc` is now a compatibility alias resolved via priority order (`bc` → `scope` → `topic` → `area` → `domain` → `context`). Response payload gains `scope` and `scope_key` fields alongside legacy `bc`. | `tools/rag/rag_tools.py` |
| 3 | **scope dictionary for `QueryDocsCached` .NET** — `QueryDocsCachedFormatter.Build`, `DeriveSourceLabel`, `FormatMarkdown` updated to `scopeFilter`/`scopeKey` parameters. `RagTools.QueryDocsCached` MCP tool now accepts `bc`, `scope`, `topic`, `area`, `domain`, `context` as alternative scope keys (same priority order). `CachedPayload` gains `Scope` and `ScopeKey` properties; projector returns `scope` and `scope_key` in JSON. | `tools/rag-dotnet/src/RagTools.Mcp/Tools/QueryDocsCachedFormatter.cs`, `RagTools.cs`, `RagToolsProjector.cs` |
| 4 | **Python E2E tests updated** — `test_ingest_e2e.py`: added `test_post_with_inline_config_only_returns_202` (proves single-file ingest works), fixed pre-existing camelCase snake_case mismatches (`operationId→operation_id`, `statusUrl→status_url`, `indexedChunks→indexed_chunks`, `errorMessage→error_message`). `test_ingest_api.py`: replaced `test_missing_metadata_rules_returns_400` and `test_missing_queries_returns_400` with inline-fallback tests; added `test_missing_metadata_rules_everywhere_returns_400`. `test_full_pipeline.py`: `_http_batch_upload` helper now uses inline rag-config (no mandatory companion injection). `tests/test_query_docs_cached.py`: updated to `scope_filter`/`scope_key` signature. | `tools/rag/test_ingest_e2e.py`, `tools/rag/tests/test_ingest_api.py`, `tools/rag/test_full_pipeline.py`, `tools/rag/tests/test_query_docs_cached.py` |
| 5 | **.NET formatter tests updated** — `QueryDocsCachedFormatterTests` adapted to new `scopeFilter`/`scopeKey` params; added `SourceLabel_GenericScope_ProducesSameLabelAsBc`, `Build_GenericScopeKey_AppearsInMarkdown`, `Build_BcScopeKey_ProducesLegacyBcArgument`. | `tools/rag-dotnet/src/RagTools.Tests/Tools/QueryDocsCachedFormatterTests.cs` |
| 6 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Test results: Python 85/85 PASSED · .NET 71/71 PASSED.

---

### Session 70 — scope_attrs map parity + full test sweep (2026-06-09)

Refactor domkniety po stronie Python i .NET oraz zweryfikowany pelnym przebiegiem testow.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | **scope_attrs dictionary parity** — `query_docs_cached` uses one generic scope map instead of hardcoded `bc`-only parameters. Python and .NET now derive cached source labels and markdown from the same dictionary-shaped scope payload. | `tools/rag/rag_tools.py`, `tools/rag-dotnet/src/RagTools.Mcp/Tools/QueryDocsCachedFormatter.cs`, `RagTools.cs`, `RagToolsProjector.cs` |
| 2 | **test sweep completed** — Python E2E passed (`test_ingest_e2e.py`), full Python RAG suite passed (`456 passed` after excluding the standalone `smoke_test.py` helper), and the .NET RAG test project passed (`532 passed`). | `tools/rag/test_ingest_e2e.py`, `tools/rag`, `tools/rag-dotnet/src/RagTools.Tests/RagTools.Tests.csproj` |
| 3 | **context-mode write attempt stayed blocked** — write-through to context-mode remained read-only, so the changelog sync was completed with the local editor path instead. | `.github/COPILOT-SETUP-CHANGELOG.md` |

Test results: Python E2E 17/17 PASSED · Python suite 456/456 PASSED · .NET 532/532 PASSED.

---

### Session 68 — spec and flow content alignment to current implementation (2026-06-05)

Workflow 11 + Workflow 7 close-out after content-only updates to existing workflow specifications and matching flow diagrams.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Updated IAM refresh-token spec to endpoint/service behavior only (`SignInAsync`, `RefreshAsync`, `RevokeAsync`) and removed synthetic event lifecycle naming. | `docs/specifications/iam-refresh-token.md` |
| 2 | Updated Orders checkout spec to real `InitiateCheckoutResult`/`CheckoutResult` outcomes and current integration messages (`OrderPlaced`, `OrderPlacementFailed`). | `docs/specifications/orders-checkout.md` |
| 3 | Updated Inventory reservation spec to actual handlers/service methods and `StockHoldStatus` values. | `docs/specifications/inventory-reservation-release.md` |
| 4 | Updated Payments lifecycle spec to implemented domain/status transitions and registered handlers/jobs/messages. | `docs/specifications/payments-lifecycle.md` |
| 5 | Updated Coupons spec to real service outcomes (`CouponApplyResult`, `CouponRemoveResult`) and implemented compensation handlers (`OrderCancelled`, `PaymentExpired`). | `docs/specifications/coupons-apply-revert.md` |
| 6 | Updated all 5 flow diagrams under `assets/diagrams/flows/` to current implementation steps only (no speculative/future branches). | `assets/diagrams/flows/*.md` |
| 7 | Verified no `.sln` structural sync required (files already present where applicable); close-out sync recorded (this entry). | `ECommerceApp.sln`, `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 9, agents 10, skills 36, ADRs 29, context files 8).

### Session 67 — session policy timeout tuning (2026-06-05)

Workflow 11 + Workflow 7 close-out after tuning MCP timeout guidance in SessionStart hook.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Changed default MCP timeout policy to fast-fail (`15000ms`) for regular calls. | `.github/hooks/session-policy.mjs` |
| 2 | Kept explicit long-operation override (`300000ms`) for ingest/build/full reindex scenarios. | `.github/hooks/session-policy.mjs` |
| 3 | Synced hooks solution items to include `session-policy.mjs`. | `ECommerceApp.sln` |
| 4 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

### Session 66 — spec-writer batch close-out: payments, inventory, coupons, iam (2026-06-05)

Workflow 11 + Workflow 7 close-out after adding the next four workflow specifications.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Added Payments lifecycle specification draft. | `docs/specifications/payments-lifecycle.md` |
| 2 | Added Inventory reservation release specification draft. | `docs/specifications/inventory-reservation-release.md` |
| 3 | Added Coupons apply-revert specification draft. | `docs/specifications/coupons-apply-revert.md` |
| 4 | Added IAM refresh-token specification draft. | `docs/specifications/iam-refresh-token.md` |
| 5 | Updated specifications index to move created specs from planned to created. | `docs/specifications/README.md` |
| 6 | Synced `.sln` specifications solution folder with all created spec files. | `ECommerceApp.sln` |
| 7 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 9, agents 10, skills 36, ADRs 29, context files 8).

### Session 65 — spec-writer close-out: orders-checkout spec mirror sync (2026-06-05)

Workflow 11 + Workflow 7 close-out after approving and adding the first workflow specification draft.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Added first created flow spec artifact in specifications area. | `docs/specifications/orders-checkout.md` |
| 2 | Updated specifications index status from planned-only to include created spec list entry. | `docs/specifications/README.md` |
| 3 | Synced solution docs tree: added the new spec file under the `specifications` solution folder. | `ECommerceApp.sln` |
| 4 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 9, agents 10, skills 36, ADRs 29, context files 8).

Not changed (deliberate):

- MCP routing semantics and precedence rules.
- Root Copilot policy sections and instruction routing tables.

### Session 64 — README hub + assets bootstrap + specifications mirror sync (2026-06-05)

Workflow 11 + Workflow 7 close-out after the first portfolio/docs cleanup step.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Refactored root README into a concise hub (quick start, stack, references to architecture/ADR/roadmap/specs). Removed long legacy screenshot dump and Docker SQL bootstrap script section. | `README.md` |
| 2 | Added lightweight assets entry points for short visual layer (no deep docs duplication). | `assets/README.md`, `assets/screens/README.md`, `assets/diagrams/README.md` |
| 3 | Added specifications index stub to establish docs target for upcoming flow specs. | `docs/specifications/README.md` |
| 4 | Synced solution docs tree: added `specifications` solution folder with `docs/specifications/README.md`. | `ECommerceApp.sln` |
| 5 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for instruction/prompt/agent/skill families; docs structure expanded with `docs/specifications/README.md` and mirrored in `.sln`.

### Session 63 — MCP routing compact policy + global cancel resilience defaults (2026-06-05)

Workflow 11 + Workflow 7 close-out for MCP routing policy consolidation and drift-safe mirroring in root instructions.

| # | Change | Files affected |
|---|---|---|
| 1 | Compressed canonical MCP routing document while preserving precedence, fail-open, and retry behavior. | `.github/instructions/mcp-routing.instructions.md` |
| 2 | Added explicit long-wait default for long MCP operations: 5-minute threshold with `timeout=300000` where supported. | `.github/instructions/mcp-routing.instructions.md` |
| 3 | Generalized cancel handling to all MCP servers (not context-mode only) with deterministic 5-step retry sequence and risk-acceptance fallback. | `.github/instructions/mcp-routing.instructions.md` |
| 4 | Strengthened context-mode runtime defaults: `javascript` first, non-`javascript` only after availability verification, auto-fallback to `javascript`/bounded `shell`. | `.github/instructions/mcp-routing.instructions.md` |
| 5 | Mirrored non-negotiable MCP defaults in root instructions (long-wait, retry contract, runtime default) to enforce setup-wide behavior across models. | `.github/copilot-instructions.md` |
| 6 | Updated canonical pointer list in root instructions to avoid brittle heading anchors after routing compaction. | `.github/copilot-instructions.md` |
| 7 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for artifact families (instructions 18, prompts 9, agents 10, skills 36, ADRs 29, context files 8).

### Session 62 — code-validator, mermaid-diagram, context-updater skills; spec-writer agent; general prompt; Progressive Disclosure §15; advisory anti-patterns (2026-06-03)

Workflow 11 + Workflow 7 close-out for batch of new Copilot configuration artifacts migrated and adapted from `.github2` (Eplan Identity Service).

| # | Change | Files affected |
|---|---|---|
| 1 | Added `code-validator` skill — fast pre-commit BLOCKS MERGE check (lighter than full `@code-reviewer`). | `.github/skills/code-validator/SKILL.md` |
| 2 | Added `mermaid-diagram` skill — GitHub + ADO wiki compatible diagram generation with dual-target guidance. | `.github/skills/mermaid-diagram/SKILL.md` |
| 3 | Added `context-updater` skill — keeps `.github/context/*.md` in sync after ADR changes using source-of-truth table. | `.github/skills/context-updater/SKILL.md` |
| 4 | Added `anti-patterns-advisory.context.md` — P2/P3 suggestions (AutoMapper in new BC, missing CancellationToken, etc.); loaded only on deep reviews, never BLOCKS MERGE evidence. | `.github/context/anti-patterns-advisory.context.md` |
| 5 | Added `spec-writer.md` agent — creates and maintains `docs/specifications/*.md` business workflow specs with HITL and RAG-first ADR lookup. | `.github/agents/spec-writer.md` |
| 6 | Replaced `specification.template.md` content with ECommerceApp-specific template (was Eplan placeholder). | `.github/templates/specification.template.md` |
| 7 | Added `context-cost-analysis.md` — per-file token budget measurements (7,110 fixed, 4,540 dotnet.instructions, etc.). | `.github/context-cost-analysis.md` |
| 8 | Added `general.prompt.md` — efficient general Q&A prompt using Tier 0-3 progressive context loading. | `.github/prompts/general.prompt.md` |
| 9 | Added §15 to `copilot-instructions.md` — Context budget (≤8K target), Progressive Disclosure (Tier 0-3), Navigation Map table. | `.github/copilot-instructions.md` |
| 10 | Updated `code-reviewer.md` — added `anti-patterns-advisory.context.md` load rule for deep reviews. | `.github/agents/code-reviewer.md` |
| 11 | Updated `docs-index.instructions.md` — added Code scaffolding skills table, `general.prompt.md`, `@spec-writer`. | `.github/instructions/docs-index.instructions.md` |
| 12 | Updated `docs-index.full.md` — added `setup-discovery` + `spec-writer` agents, 5 missing prompts, ADRs 0027–0029, advisory context row, test-stabilization-policy row, and 3 new skills in organized subsections. | `.github/instructions/docs-index.full.md` |
| 13 | Updated `AGENT-PIPELINE.md` — added `@spec-writer` to domain agents table. | `.github/AGENT-PIPELINE.md` |
| 14 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: instructions 18, prompts 9 (+1), agents 10 (+1), skills 36 (+3), ADRs 29, context files 8 (+1).

### Session 61 — context-mode docs sync to v1.0.161 runtime details (2026-06-01)

Workflow 11 + Workflow 7 close-out for small documentation drift after context-mode runtime/logging changes.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Updated getting-started version references from context-mode `v1.0.151` to `v1.0.161`. | `docs/getting-started-context-mode.md` |
| 2 | Added VS Code hook debug log tail commands (`/home/node/.vscode/context-mode/*.log`) to setup guide. | `docs/getting-started-ai-mcp-stack.md` |
| 3 | Synced roadmap technical snippets to current runtime defaults (`node:24`, `CONTEXT_MODE_NODE_OPTIONS`, `CONTEXT_MODE_DEBUG`) and hook command shape. | `docs/roadmap/context-mode-details.md` |
| 4 | Added missing roadmap entry for `context-mode-details.md` to full docs index table. | `.github/instructions/docs-index.full.md` |
| 5 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- MCP routing semantics and precedence rules.
- Runtime container hardening profile and compose topology.

### Session 60 — human-friendly setup guides + Docker recreate runbook (2026-06-01)

Workflow 11 + Workflow 7 close-out for onboarding simplification. Added an explicit step-by-step guide for context-mode + RAG setup paths and linked it from docs navigation.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Added a practical setup guide for context-mode and RAG covering ingest + STDIO/HTTP in local/source and container modes. | `docs/getting-started-ai-mcp-stack.md` |
| 2 | Added docs entry-point link for the new guide. | `docs/README.md` |
| 3 | Added guide to solution docs items for discoverability. | `ECommerceApp.sln` |
| 4 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- MCP routing semantics and fail-open rules.
- Container security hardening profile.

### Session 59 — post-audit mirror alignment (2026-06-01)

Workflow 11 + Workflow 7 follow-up after Session 58 audit to remove remaining mirror drift.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Synced `.sln` prompt solution items with current prompt inventory (added missing 4 prompt files). | `ECommerceApp.sln` |
| 2 | Added missing `test-stabilization-policy.md` to `.sln` context solution items. | `ECommerceApp.sln` |
| 3 | Updated roadmap hook JSON snippet to match runtime hook stderr redirection (`hooks.log`). | `docs/roadmap/context-mode-details.md` |
| 4 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- MCP routing semantics and tool precedence.
- Runtime container profile layout.

### Session 58 — context-mode container log visibility (2026-06-01)

Workflow 11 + Workflow 7 close-out for operational logging visibility. Root cause: context-mode service runs idle (`sleep infinity`) and MCP/hook processes are launched via `docker exec`, so stderr was not visible in `docker logs`.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Updated context-mode service entrypoint to keep idle architecture but tail persistent runtime/hook/network log files into container stdout for `docker logs` visibility. | `docker-compose.yaml` |
| 2 | Redirected MCP server stderr to `/home/ctxmode/.context-mode/runtime.log` (stdout unchanged to preserve stdio protocol). | `.vscode/mcp.json` |
| 3 | Redirected context-mode hook stderr to `/home/ctxmode/.context-mode/hooks.log` for unified container log stream. | `.github/hooks/context-mode.json` |
| 4 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- MCP routing semantics and fallback rules.
- Source tree layout and profile structure.

### Session 57 — Final A+B+C close-out and mirror drift fixes (2026-06-01)

Workflow 11 + Workflow 7 finalization after Stage 1/2/3 slimming: resolved remaining mirror drift found in close-out audit.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Fixed tool-name drift in full index table (`get_adr_history` -> `get_history`) for consistency with canonical routing. | `.github/instructions/docs-index.full.md` |
| 2 | Added `docs-index.full.md` to solution instruction items to keep .sln mirror complete. | `ECommerceApp.sln` |
| 3 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- Rule semantics and runtime behavior.
- File structure (no moves, no new config categories).

### Session 56 — Stage 3 selective applyTo narrowing (2026-06-01)

Workflow 11 + Workflow 7 close-out for B-stage context reduction: narrowed one low-risk global instruction scope to reduce startup context load without touching critical global guardrails.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Narrowed `rag.instructions.md` from global `applyTo: "**"` to docs/setup/rag-relevant paths only (`.github/**, docs/**, tools/rag/**, tools/rag-dotnet/**`). | `.github/instructions/rag.instructions.md` |
| 2 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- Critical global guardrails remain global (`mcp-routing`, `pre-edit`, `safety`, `agent-memory`, `batched-tasks`).
- File structure and locations — no new files or moves.

### Session 55 — Stage 2 slimming of pre-edit/doc-suggestions (2026-06-01)

Workflow 11 + Workflow 7 close-out for C-stage context reduction: compressed high-frequency global instructions while preserving mandatory triggers and post-edit obligations.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Rewrote pre-edit checklist into compact rule-only format; preserved mandatory pre-read, MCP-first routing constraints, clarification policy, capability verification, and post-edit maintainer requirement. | `.github/instructions/pre-edit.instructions.md` |
| 2 | Rewrote proactive doc-suggestions into compact trigger matrix; preserved all suggestion categories and strict suggest-only policy. | `.github/instructions/doc-suggestions.instructions.md` |
| 3 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- File structure and locations — no new files or moves.
- Rule intent — semantics preserved, only text volume reduced.

### Session 54 — Stage 1 slimming of canonical MCP routing (2026-06-01)

Workflow 11 + Workflow 7 close-out for A-stage context reduction: compressed canonical MCP routing text while preserving routing semantics and mandatory guardrails.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Rewrote canonical routing into compact "core + operational minimum + pointers" form; removed long narrative/examples while preserving all critical rules (precedence, invalid-answer, empty-result retries, canceled fail-open, pre-dispatch guard, end-of-run `ctx_stats`). | `.github/instructions/mcp-routing.instructions.md` |
| 2 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- File structure and locations — no new files or moves.
- Rule intent — semantics preserved, only text volume reduced.

### Session 53 — mandatory end-of-run `ctx_stats` output (2026-06-01)

Workflow 11 + Workflow 7 close-out after repeated runs where context-mode was used but raw `ctx_stats` was missing from the final response.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Canonical routing now requires end-of-run `ctx_stats` whenever any `ctx_*` tool was used; raw output must be shown in final response. | `.github/instructions/mcp-routing.instructions.md` |
| 2 | Root MCP summary mirrors the same requirement for fast and consistent agent behavior. | `.github/copilot-instructions.md` |
| 3 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- File structure and locations — no new files or moves.
- Runtime/container setup — policy-only telemetry requirement.

### Session 52 — pre-dispatch guard for known canceled shapes (2026-06-01)

Workflow 11 + Workflow 7 close-out after observing that some diagnostic runs still dispatched known cancellation-prone heavy shell scans to context-mode.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Canonical routing now requires a pre-dispatch short-circuit for known-bad `ctx_execute(shell)` shapes: do not run, emit explicit inability marker, and continue with safe rewritten shape or downstream step. | `.github/instructions/mcp-routing.instructions.md` |
| 2 | Root MCP summary mirrors the same known-bad shape guard requirement. | `.github/copilot-instructions.md` |
| 3 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- File structure and locations — no new files or moves.
- Runtime/container setup — policy-only guardrail hardening.

### Session 51 — fail-open canceled handling contract (2026-06-01)

Workflow 11 + Workflow 7 close-out for resilience hardening after repeated diagnostic runs where heavy context-mode steps canceled and the run quality degraded.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Canonical routing now includes a mandatory fail-open response contract for unrecoverable canceled steps: explicit inability marker (`UNABLE_TO_PROCESS`) + reason + continued next step. | `.github/instructions/mcp-routing.instructions.md` |
| 2 | Root MCP summary mirrors the graceful degradation requirement so agents continue independent steps instead of failing fast. | `.github/copilot-instructions.md` |
| 3 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- File structure and locations — no new files or moves.
- Runtime/container setup — policy-only behavior hardening.

### Session 50 — in-place routing governance cleanup (no structure change) (2026-06-01)

Workflow 11 + Workflow 7 close-out for maintainability hardening after repeated policy additions. Goal: reduce patchwork risk without changing file layout or dropping any rule.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Added explicit change-management contract in canonical MCP routing: stable core vs tunable operational zones, plus non-regression checklist. | `.github/instructions/mcp-routing.instructions.md` |
| 2 | Reduced root-policy duplication in MCP section; kept non-negotiable summary and replaced long duplicate text with pointers to canonical sections. | `.github/copilot-instructions.md` |
| 3 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- File structure and locations — no new instruction files, no file moves.
- Runtime behavior — routing logic preserved; this pass improves maintainability and drift resistance.

### Session 49 — canceled-trigger guardrails for heavy shell scans (2026-06-01)

Workflow 11 + Workflow 7 close-out after recurring cancellations on intentionally heavy benchmark step-A commands (`find /workspace` + per-file `grep`/`sha256sum` loops).

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Canonical routing now documents cancellation-prone command anti-patterns and a required rewrite order (scope bound → extension filters → javascript reducer → split calls). | `.github/instructions/mcp-routing.instructions.md` |
| 2 | Root policy mirrors the same guidance so benchmark prompts avoid pathological shell shapes that cancel before retry/fallback logic can help. | `.github/copilot-instructions.md` |
| 3 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- Application code and tests — policy-only update.
- Runtime/container wiring — no compose or hook changes required.

### Session 48 — canceled-call recovery policy in setup routing (2026-06-01)

Workflow 11 + Workflow 7 close-out after repeated test feedback that `Canceled` tool calls were treated as terminal and aborted analysis/benchmark runs.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Canonical routing now defines `Canceled` as recoverable: max 3 retries with lighter call shape, then one fallback path, mandatory partial report if still incomplete. | `.github/instructions/mcp-routing.instructions.md` |
| 2 | Root policy mirrors the same `Canceled` recovery behavior in the non-negotiable summary so agents do not fail-fast on canceled `ctx_*` calls. | `.github/copilot-instructions.md` |
| 3 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- Application code and tests — policy-only update.
- Runtime/container wiring — no command or compose changes required.

### Session 47 — context-benchmark integrity gate (`ctx_stats`) (2026-06-01)

Workflow 11 + Workflow 7 close-out after test feedback showed KPI/report inconsistencies (for example claimed multi-step `ctx_*` usage vs raw `ctx_stats` showing `0 calls`). Added an explicit integrity gate so benchmark outputs are either source-of-truth aligned or marked invalid.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Canonical MCP routing gained a benchmark-integrity section: `ctx_stats` is mandatory KPI source of truth, mismatches (`0 calls` vs claimed usage) invalidate the run, and chat-session transport artifacts are forbidden as KPI evidence. | `.github/instructions/mcp-routing.instructions.md` |
| 2 | Root policy now mirrors the same integrity guard in the non-negotiable summary for fast agent pickup. | `.github/copilot-instructions.md` |
| 3 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- Application code and tests — policy-only update.
- MCP runtime wiring and hooks — no command-shape change needed; this pass tightens answer integrity only.

### Session 46 — intent-based automatic context-mode routing wording (2026-06-01)

Workflow 11 + Workflow 7 close-out after clarifying that users should describe the task in natural language and the agent must infer context-mode automatically, instead of waiting for explicit `ctx_*` phrasing in prompts.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Canonical MCP routing now states explicitly that users do not need to name `ctx_*` tools or mention context-mode; routing is inferred from intent and expected output shape. | `.github/instructions/mcp-routing.instructions.md` |
| 2 | Root Copilot instructions now say natural-language task descriptions are sufficient and explicit sandbox/tool wording is optional. | `.github/copilot-instructions.md` |
| 3 | Pattern doc TL;DR now opens with "describe the job, not the tool" plus concrete examples mapping plain-language asks to the automatic path. | `docs/patterns/context-mode-read-write-split.md` |
| 4 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- Application code and tests — policy-only update.
- `.vscode/mcp.json` and `.github/hooks/context-mode.json` — runtime wiring already auto-detects the workspace correctly; this pass only tightened prompt/routing semantics.

### Session 45 — context-mode workspace-aware startup wrapper (2026-06-01)

Workflow 11 + Workflow 7 close-out after fixing context-mode startup so the tool derives its workspace context automatically instead of requiring payloads to hardcode `/workspace`.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | VS Code MCP registration now starts context-mode through `sh -lc`, resolves `$CONTEXT_MODE_WORKSPACE` inside the container, changes directory there, then launches `node --require /app/network-monitor.cjs /app/cli.bundle.mjs`. | `.vscode/mcp.json` |
| 2 | Context-mode hook commands (`PreToolUse`, `UserPromptSubmit`, `PreCompact`, `SessionStart`) now run through the same workspace-aware shell wrapper before invoking `node /app/cli.bundle.mjs hook ...`. | `.github/hooks/context-mode.json` |
| 3 | Roadmap/details and bootstrap docs updated to replace stale `node ... serve` / direct hook invocations with the live workspace-aware wrapper shape. | `docs/roadmap/context-mode-details.md`, `docs/playbooks/context-mode-bootstrap.md`, `.github/skills/setup-mcp-clients/SKILL.md`, `.github/skills/setup-context-mode-new-project/SKILL.md` |
| 4 | Validation: container-side wrapper resolves `pwd` to `/workspace` and prints the same value from `$CONTEXT_MODE_WORKSPACE`; edited JSON files are error-free. | `.vscode/mcp.json`, `.github/hooks/context-mode.json` |
| 5 | Helper smoke-test scripts now use the same workspace-aware wrapper as MCP startup before calling `cli.bundle.mjs`, and active guides/skills were updated to use `ctx_execute("shell", ...)` plus the exact bootstrap/start/test/stop command set. | `scripts/test-mcp-handshake.ps1`, `scripts/test-ctx-doctor.ps1`, `scripts/test-ctx-fetch.ps1`, `scripts/test-mcp-handshake.sh`, `scripts/test-ctx-doctor.sh`, `scripts/test-ctx-fetch.sh`, `docs/getting-started-context-mode.md`, `docs/reference/context-mode-tools.md`, `docs/patterns/context-mode-read-write-split.md`, `docs/roadmap/context-mode-integration.md`, `docs/adr/0029/0029-context-mode-mcp-sandbox.md`, `.github/skills/ctx-bootstrap-network/SKILL.md`, `.github/skills/ctx-doctor-playbook/SKILL.md`, `.github/skills/rag-eval-coverage/SKILL.md`, `.github/skills/rag-with-memory/SKILL.md` |
| 6 | Visual Studio / solution discoverability sync: added the hooks solution folder and mirrored the updated getting-started and reference docs into `ECommerceApp.sln`. | `ECommerceApp.sln` |
| 7 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- `docker-compose.yaml` — mount and env wiring were already parametric via `${CONTEXT_MODE_WORKSPACE:-/workspace}`; the fix was in the process launch path, not the container definition.
- `Dockerfile-context-mode` — runtime image already exposed `/app/cli.bundle.mjs`, `WORKDIR /workspace`, and `network-monitor.cjs`; no image rebuild logic change required.

### Session 44 — Roadmap RAG Phase 3 completion status alignment close-out (2026-05-31)

Workflow 11 + Workflow 7 compliance pass for docs-only roadmap status alignment.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Aligned roadmap index RAG row status with the detailed remote multi-tenant roadmap to mark Phase 3 complete. | `docs/roadmap/README.md` |
| 2 | Close-out sync check: no routing-index, root policy, or solution-structure updates required. | `.github/instructions/docs-index.instructions.md`, `.github/copilot-instructions.md`, `ECommerceApp.sln` |
| 3 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- `.github/instructions/docs-index.instructions.md` — no new/renamed roadmap artifact and no docs routing table delta.
- `.github/copilot-instructions.md` — repo-level policy and routing summary remain valid.
- `ECommerceApp.sln` — `docs/roadmap/README.md` and `docs/roadmap/rag-remote-multitenant.md` are already present in solution items.

### Session 43 — sandbox-first analysis routing update (2026-05-31)

Workflow 11 + Workflow 7 compliance pass after making context-mode the default route for analysis tasks.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Canonical MCP routing now explicitly lists analysis trigger words (analyze, summarize, count, compare, grep, parse, transform, extract, search) and routes them to `ctx_execute_file` / `ctx_execute` / `ctx_search` by default. | `.github/instructions/mcp-routing.instructions.md` |
| 2 | Root Copilot instructions now include a sandbox-first shortcut so analysis tasks automatically bias toward context-mode. | `.github/copilot-instructions.md` |
| 3 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- `docs-index.instructions.md` — no new/renamed config artifact.
- Application code and tests — policy-only update.

### Session 42 — docs path-hardcoding cleanup close-out (2026-05-31)

Workflow 11 + Workflow 7 compliance pass after removing remaining host-specific absolute path examples from docs.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Replaced host-specific absolute-path example with a generic cross-platform warning in ADR-0027 (`C:\...`, `/Users/...`, `/home/...`). | `docs/adr/0027/0027-rag-pipeline-design.md` |
| 2 | Replaced Scheduled Task argument absolute path with a repo-root placeholder in context-mode details. | `docs/roadmap/context-mode-details.md` |
| 3 | Close-out sync check: no docs-index, root policy, or solution-structure updates required (both files already mirrored in solution items). | `ECommerceApp.sln` |
| 4 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- `.github/instructions/docs-index.instructions.md` — no new/renamed docs artifacts and no routing-index delta.
- `.github/copilot-instructions.md` — policy/routing summary remains valid.
- Application code and tests — docs-only cleanup.

### Session 41 — cross-platform path normalization (relative-first) follow-up (2026-05-31)

Workflow 11 + Workflow 7 follow-up pass to remove host-specific path examples and make `ctx_execute_file` guidance platform-agnostic.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Canonical routing switched from host-specific examples to relative-first guidance: start from repo-relative path, map to `$CONTEXT_MODE_WORKSPACE` (default `/workspace`), normalize separators to `/`, and avoid host absolute paths on all platforms. | `.github/instructions/mcp-routing.instructions.md` |
| 2 | Root policy summary updated to the same cross-platform rule (no host absolute paths, relative-first mapping). | `.github/copilot-instructions.md` |
| 3 | Doctor playbook error row generalized from Windows-only case to cross-platform host-absolute path failures (`C:\...`, `/Users/...`, `/home/...`). | `.github/skills/ctx-doctor-playbook/SKILL.md` |
| 4 | Pattern doc examples switched from host absolute paths to repo-relative input examples and explicit "do not pass host absolute paths" guidance. | `docs/patterns/context-mode-read-write-split.md` |
| 5 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- `.github/instructions/docs-index.instructions.md` — no new/renamed artifacts.
- `ECommerceApp.sln` — no new files in this follow-up.
- Application code and tests — documentation/policy scope only.

### Session 40 — context-mode path normalization guardrails (`ctx_execute_file`) (2026-05-31)

Workflow 11 + Workflow 7 compliance pass after fixing the recurring host-path vs container-path issue for context-mode file execution.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Canonical MCP routing now enforces mandatory path normalization for `ctx_execute_file`: never pass Windows host paths; convert `<repo-root>/<relative>` to `/workspace/<relative>` (or `$CONTEXT_MODE_WORKSPACE/<relative>`). Includes explicit conversion examples and one-shot mount probe command. | `.github/instructions/mcp-routing.instructions.md` |
| 2 | Root policy summary now includes the same non-negotiable path normalization rule so new sessions inherit it immediately. | `.github/copilot-instructions.md` |
| 3 | Pipeline spec now includes an explicit context-mode path rule for all agents using `ctx_execute_file`. | `.github/AGENT-PIPELINE.md` |
| 4 | Doctor playbook now maps `ENOENT` with `/workspace/c:\...` to the exact root cause and conversion fix. | `.github/skills/ctx-doctor-playbook/SKILL.md` |
| 5 | Pattern doc now includes explicit host-path (wrong) → container-path (correct) examples and mount probe guidance. | `docs/patterns/context-mode-read-write-split.md` |
| 6 | Solution mirror synced: added missing patterns SolutionItem entry for the updated context-mode pattern doc. | `ECommerceApp.sln` |
| 7 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- `.github/instructions/docs-index.instructions.md` — no new/renamed instruction, agent, prompt, or skill artifact.
- Application code and tests — documentation/policy scope only.

### Session 39 — Roadmap close-out after Orders/Payments roadmap refinements (2026-05-31)

Workflow 11 + Workflow 7 compliance pass after additional roadmap documentation edits in roadmap index and atomic-switch tracks.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Updated roadmap index status/details for Orders and Payments atomic-switch tracks and refreshed review timestamp. | `docs/roadmap/README.md` |
| 2 | Updated Orders atomic-switch roadmap cleanup/acceptance status and review stamp. | `docs/roadmap/orders-atomic-switch.md` |
| 3 | Updated Payments atomic-switch roadmap cleanup/acceptance status and review stamp. | `docs/roadmap/payments-atomic-switch.md` |
| 4 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- `.github/instructions/docs-index.instructions.md` — no new/renamed roadmap artifacts and no routing-index delta.
- `.github/copilot-instructions.md` — policy/routing summary remains valid.
- `ECommerceApp.sln` — edited roadmap files were already present in solution items.

### Session 38 — ADR-0029 acceptance + roadmap ADR alignment close-out (2026-05-31)

Workflow 11 + Workflow 7 compliance pass after promoting ADR-0029 status and aligning the context-mode roadmap row with its ADR.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Promoted ADR-0029 main document status from Draft/Proposed to Accepted. | `docs/adr/0029/0029-context-mode-mcp-sandbox.md` |
| 2 | Synced ADR-0029 folder router/readme status to Accepted. | `docs/adr/0029/README.md` |
| 3 | Added ADR link in the context-mode roadmap row to keep roadmap↔ADR mapping explicit. | `docs/roadmap/README.md` |
| 4 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

Counts: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

Not changed (deliberate):

- `.github/instructions/docs-index.instructions.md` — no new ADR folder/file, rename, or routing table delta.
- `.github/copilot-instructions.md` — policy/routing summary remains valid.
- `ECommerceApp.sln` — changed docs were already present in solution items.

### Session 37 — Standalone/global RAG playbook + docs sync close-out (2026-05-29)

Workflow 11 + Workflow 7 compliance pass after adding a standalone/global RAG deployment guide for reuse in other repositories.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Added a new playbook covering standalone multi-project RAG rollout, isolation rules, June switch readiness checks, and future Ollama intent-router layering. | `docs/playbooks/rag-standalone-global.md` _(new)_ |
| 2 | Synced human-facing docs navigation to expose the new playbook from root docs, playbook hub, and RAG README. | `docs/README.md`, `docs/playbooks/README.md`, `docs/rag/README.md` |
| 3 | Synced Copilot routing/docs mirrors and solution items for the new playbook. | `.github/instructions/docs-index.instructions.md`, `.github/instructions/docs-index.full.md`, `ECommerceApp.sln` |
| 4 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

**Counts**: unchanged for configuration artifact families (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

**Not changed (deliberate)**:

- `copilot-instructions.md` — still correctly delegates detailed docs routing to `docs-index.instructions.md`.
- ADRs — the new content is operational/bootstrap guidance, not a new accepted architectural decision.
- Application code and tests.

### Session 36 — RAG requirements audit report + setup sync close-out (2026-05-29)

Workflow 11 + Workflow 7 compliance pass after adding a new RAG requirements/compliance report.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Added consolidated requirements/decisions/compliance audit report for RAG (requirements checklist, decision trace, code evidence, remaining gaps). | `docs/reports/rag-requirements-compliance-2026-05-29.md` _(new)_ |
| 2 | Synced solution items for reports folder to include the new report file. | `ECommerceApp.sln` |
| 3 | Close-out sync recorded (this entry). | `.github/COPILOT-SETUP-CHANGELOG.md` |

**Counts**: unchanged for configuration artifacts (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

**Not changed (deliberate)**:

- `.github/instructions/docs-index.instructions.md` — no routing/index-table impact.
- Application code — report and configuration-only maintenance.

### Session 35 — Roadmap sync close-out after RAG stabilization docs update (2026-05-29)

Workflow 11 + Workflow 7 compliance pass after updating roadmap docs for dual-stack RAG stabilization progress.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | Roadmap index updated with explicit Remote Multi-Tenant RAG row and current status summary (Phase 3 mostly complete, P3-8 pending, 2026-05-29 stabilization/validation complete). | `docs/roadmap/README.md` |
| 2 | RAG roadmap updated with a dated stabilization section documenting completed config simplification outcomes, validation matrix, and unchanged pending item P3-8. | `docs/roadmap/rag-remote-multitenant.md` |
| 3 | Copilot setup close-out recorded (this entry) — no docs-index, solution, instruction, prompt, agent, or skill structure changes required. | `.github/COPILOT-SETUP-CHANGELOG.md` |

**Counts**: unchanged for this session's edits (instructions 18, prompts 8, agents 9, skills 33, ADRs 29, context files 7).

**Not changed (deliberate)**:

- `.github/instructions/docs-index.instructions.md` — routing/index table unchanged.
- `ECommerceApp.sln` — roadmap files were already present in solution items.
- Any application code or tests.

### Session 34 — ADR-0028 Amendment 005: Phase 3 per-collection config .NET delivery (2026-05-28)

**Why**: Ship Phase 3 (P3-1..P3-6 + P3-X follow-up) on the .NET RAG server — per-collection config persistence (Design B: indexer writes `RagConfigPayload` into Qdrant `__config__` point; query side reads through `IConfigSource` with mode switch `RAG_CONFIG_SOURCE` = `file` / `qdrant` / `layered`, wrapped by `CachingConfigSource`). Closes ADR-0028 Amendment 004's documented gap on the .NET side. Python mirror tracked as roadmap step P3-7.

| #   | Change                                                                                                                                                                                                          | Files                                                                                                                                              |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | New ADR-0028 amendment 005 documenting Design B scope (chunker tokens, ranking weights, history field), payload schema, mode switch, and final P3-X resolved / P3-Y withdrawn outcomes                          | `docs/adr/0028/amendments/0028-005-phase3-per-collection-config-dotnet.md` _(new)_, `docs/adr/0028/README.md` _(amendment row + 004 forward pointer)_ |
| 2   | sln backfill: add amendment 005 to ADR-0028 `amendments` SolutionItems (continuation of the 001–004 listing)                                                                                                    | `ECommerceApp.sln`                                                                                                                                  |

**Counts**: no change. ADRs still 29 (amendment under existing ADR-0028). No new instruction / prompt / agent / skill files.

**Tests**: 525/525 green on `tools/rag-dotnet/RagTools.sln` after P3-X (`2e401c28`).

**Not changed (deliberate)**:

- `docs-index.instructions.md` — ADR-0028 routed via folder router; amendments not listed individually by convention.
- No pipeline agent / instruction changes — Workflow 12 not triggered.
- `copilot-instructions.md` unchanged — no new BC.

**Open for next session (P3-7 Python mirror)**:

- Python server still reads ranking weights & chunker tokens from `rag-config.yaml` only.
- Re-run `tools/rag/compare_queries.py` before AND after P3-7 to attribute deltas cleanly.
- Schema lock-in: payload key names chosen in amendment 005 become the cross-server contract once Python writes them. Rename later = full re-index on both collections.
- After P3-7 lands, Amendment 004's framing should be updated (or marked superseded by 005 + Python sibling).

**Refs**: branch `feat/rag-phase3-per-collection-config`, commits `ccd53f1a`, `6413b968`, `7cf09880`, `157b2cf9`, `2e401c28`.

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

### Session 30.1 — Cross-platform hook migration: `posttooluse-chain.mjs` replaces `.ps1` (2026-05-28)

Supersedes Session 30 item 2. The PostToolUse fan-out wrapper is now `posttooluse-chain.mjs` (Node.js ESM, cross-platform) instead of `posttooluse-chain.ps1` (PowerShell-only). `context-mode.json` uses `"cwd": "."` (VS Code hooks property, relative to repo root) so `node .github/hooks/posttooluse-chain.mjs` resolves correctly on all OSes. A bash fallback (`posttooluse-chain.sh`) covers headless Linux/macOS environments.

| # | Change | Files affected |
| --- | --- | --- |
| 1 | `posttooluse-chain.mjs` (Node.js ESM) replaces `posttooluse-chain.ps1`. Cross-platform fan-out wrapper; always `process.exit(0)`. | `.github/hooks/posttooluse-chain.mjs` _(new)_ |
| 2 | `posttooluse-chain.sh` — Bash fallback for headless Linux/macOS (SSH remotes, CI). | `.github/hooks/posttooluse-chain.sh` _(new)_ |
| 3 | `context-mode.json` PostToolUse: `powershell … posttooluse-chain.ps1` → `node .github/hooks/posttooluse-chain.mjs` with `"cwd": "."`. | `.github/hooks/context-mode.json` |
| 4 | 6 bash `.sh` scripts added (hook fallback + scripts): `tools/rag/run-tests.sh`, `tools/rag/run-all-tests.sh`, `tools/rag-dotnet/run-tests.sh`, `tools/rag-dotnet/download-model.sh`, `scripts/test-ctx-doctor.sh`, `scripts/test-ctx-fetch.sh`. | Various `.sh` _(new)_ |
| 5 | `auto-cache-hook.md` diagram and file table updated: `.mjs` primary, `.sh` bash fallback, `.ps1` removed. | `docs/rag/auto-cache-hook.md` |
| 6 | ADR-0029 Amendment 1 files table updated to match. | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` |
| 7 | `SETUP-GUIDE.md` and `getting-started-context-mode.md` updated with bash blocks alongside pwsh. | `docs/rag/SETUP-GUIDE.md`, `docs/getting-started-context-mode.md` |

**Not changed (deliberate)**:

- `docs-index.instructions.md`, `copilot-instructions.md`, `mcp-routing.instructions.md` — implementation-level change only; routing rules unchanged.
- `ECommerceApp.sln` — hook scripts remain runtime config, no project membership.
- Counts unchanged: instructions 17, agents 8, skills 17, ADRs 29.

**Refs**: commits `e8e4fa02`, `84167311`.

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

### Session 32 — Sprint 2 RAG quick wins: parity audit + 4 maintenance skills + ADR-0028 Amendment 4 + roadmap Phase 3 (2026-05-28)

Sprint 2 of the cross-project RAG/context-mode reusability initiative ([plan](../docs/reports/rag-context-mode-skills-plan-2026-05-28.md)). Single commit `ef7f98aa` on branch `feat/sprint1-rag-quick-wins-skills-plan`. Phase work: (1) parity audit between Python (:3002) and .NET (:3001) HTTP servers — 26 queries, 10/26 top-1 match; (2) three remediations applied (R1 .NET weights, R2 .NET amendments down-weight, R3 glossary mirror sync); (3) one remediation REJECTED (R4 Python amendments down-weight — weight delta too small vs raw cosine gap); (4) two .NET service-layer fixes (indexer Bug A+B partial, B2 `MaxTopK` 20→45); (5) 4 new RAG maintenance skills closing reindex / rebuild / debug / multilang-test gaps; (6) ADR-0028 Amendment 4 + roadmap Phase 3 documenting the per-collection config persistence gap; (7) Sprint 3+4 handoff brief for the next session.

| #   | Change | Files affected |
| --- | ------ | -------------- |
| 1   | +4 RAG maintenance skills: `rag-reindex-decision` (decision matrix before editing rag config), `rag-collection-rebuild` (destructive Qdrant drop + rebuild), `rag-query-debug` (hypothesis-ordered query diagnosis using `probe_weights.py` + `compare_queries.py`), `rag-multilang-test` (verify glossary entries on both HTTP servers) | `.github/skills/rag-reindex-decision/SKILL.md` _(new)_, `.github/skills/rag-collection-rebuild/SKILL.md` _(new)_, `.github/skills/rag-query-debug/SKILL.md` _(new)_, `.github/skills/rag-multilang-test/SKILL.md` _(new)_ |
| 2   | New ADR-0028 amendment documenting the per-collection config persistence gap (documentation-only — fix tracked in roadmap Phase 3). Linked from ADR-0028 README. | `docs/adr/0028/amendments/0028-004-per-collection-config-gap.md` _(new)_, `docs/adr/0028/README.md` |
| 3   | New roadmap Phase 3 (steps P3-1..P3-8) — per-collection config persistence fix plan, mirrored across .NET and Python servers | `docs/roadmap/rag-remote-multitenant.md` |
| 4   | 4 new reports (EN): auto-generated parity audit (26 queries), parity findings (11 sections incl. §9 Q-PRECISE, §10 indexer, §11 MaxTopK), fix-diagnosis (10 sections incl. §10 R4 falsified-hypothesis writeup), Sprint 3+4 handoff brief | `docs/reports/rag-parity-audit-2026-05-28.md` _(new)_, `docs/reports/rag-parity-findings-2026-05-28.md` _(new)_, `docs/reports/rag-parity-fix-diagnosis-2026-05-28.md` _(new)_, `docs/reports/sprint3-sprint4-requirements-brief.md` _(new)_ |
| 5   | RAG source/config (parity remediation): Python queries + probe CLI + R4-reverted config; .NET R1/R2 weights + R3 glossary mirror sync; .NET indexer Bug A+B partial fix (5/6 ADRs corrected, ADR-0028 anomaly logged); B2 `MaxTopK` 20→45 (478 unit tests pass); docker-compose mounts canonical glossary on both servers | `tools/rag/{rag-config.yaml,compare_queries.py,queries.yaml,probe_weights.py}`, `tools/rag-dotnet/{rag-config.yaml,multilingual-glossary.yaml,src/RagTools.Core/QdrantDocumentStore.cs,src/RagTools.Core/Query/RagQueryService.cs,src/RagTools.Mcp/Tools/RagTools.cs}`, `docker-compose.yaml` |
| 6   | docs-index: 4 new rows for the RAG maintenance skills appended to "RAG maintenance skills" table | `.github/instructions/docs-index.instructions.md` |
| 7   | rag.instructions.md: "Which maintenance skill to load" table cascaded with 4 new rows | `.github/instructions/rag.instructions.md` |

**Counts**: skills 20 → 24. Instructions, agents, prompts unchanged in shape (Amendment 4 lives inside ADR-0028 folder, not a new ADR).

**Not changed (deliberate, per Sprint 2 scope)**:

- R4 (Python amendments weight 1.20 → 1.10) reverted — parity unchanged, root cause is raw-cosine gap not weight delta. Documented in fix-diagnosis §10 with 3 follow-up options.
- ADR-0028 indexer anomaly (`main_file` + `amendments:33`) deferred — logged in `/memories/repo/rag-mcp-anomalies.md` entry #10.
- Amendment 4 is intentionally "deferred design intent" — Phase 3 (P3-1..P3-8) ships the actual fix in a later sprint.
- No agent files touched → no `code-reviewer.md` cascade.
- No pipeline shape change → no `AGENT-PIPELINE.md` update.

**Pre-existing drift (surfaced again, NOT auto-fixed)**:

1. ADR-0028 parent folder still missing from `ECommerceApp.sln` `adr` SolutionItems — flagged in Sessions 24, 27, 31. Sprint 2 added Amendment 4 to that folder. Same applies to ADR-0027.
2. `docs/roadmap/rag-remote-multitenant.md` missing from the `roadmap` sln SolutionItems — surfaced now because Sprint 2 added Phase 3 to it.
3. Recommendation: ship the ADR-0027 + ADR-0028 + `rag-remote-multitenant.md` sln backfill as a dedicated Workflow 10 close-out (not bundled here to keep Sprint 2 scope tight).

**Refs**: commit `ef7f98aa`, branch `feat/sprint1-rag-quick-wins-skills-plan`, `docs/reports/sprint3-sprint4-requirements-brief.md`, `/memories/repo/rag-mcp-anomalies.md` entries #8 #9 #10.

---

### Session 33 — Sprint 3+4 cross-project bootstrap skills + maintainer evolver pass (2026-05-28)

Sprints 3 and 4 of the cross-project RAG/context-mode reusability initiative ([plan](../docs/reports/rag-context-mode-skills-plan-2026-05-28.md), [handoff brief](../docs/reports/sprint3-sprint4-requirements-brief.md)). Single commit on branch `feat/sprint1-rag-quick-wins-skills-plan`, NOT pushed. Goal: make RAG + context-mode reusable in a fresh repository through composable skills + end-to-end playbooks, and teach `@copilot-setup-maintainer` to detect codebase evolution drift.

Phase 3 (ADR-0028 Amendment 4 per-collection config persistence fix, roadmap steps P3-1..P3-8) was DEFERRED — scope too large for one session. D2/E1/E2 skills carry an explicit `KNOWN GAP` blockquote pointing at the roadmap.

| #   | Change | Files affected |
| --- | ------ | -------------- |
| 1   | +9 new skills (cross-project bootstrap): D1 `ctx-bootstrap-network` (AdGuard allowlist), D2 `ctx-bootstrap-storage` (Qdrant + FTS5 + KNOWN GAP), D3 `ctx-bootstrap-runtimes` (sandbox runtime install), E1 `setup-rag-new-project` (RAG end-to-end + KNOWN GAP), E2 `setup-context-mode-new-project` (context-mode end-to-end + KNOWN GAP), E3 `setup-adguard-policy` (AdGuard stand-up), E4 `setup-mcp-clients` (VS Code / Copilot Web / VS 17.14+ matrix), E5 `setup-auto-cache-hook` (L3 hook install), B10 `rag-eval-coverage` (`comm -23` audit + priority heuristic) | `.github/skills/{ctx-bootstrap-network,ctx-bootstrap-storage,ctx-bootstrap-runtimes,setup-rag-new-project,setup-context-mode-new-project,setup-adguard-policy,setup-mcp-clients,setup-auto-cache-hook,rag-eval-coverage}/SKILL.md` _(all new)_ |
| 2   | +2 bootstrap playbooks + hub README composing the skills end-to-end with stage gates and troubleshooting flowcharts | `docs/playbooks/README.md` _(new)_, `docs/playbooks/context-mode-bootstrap.md` _(new, 7 stages)_, `docs/playbooks/rag-bootstrap.md` _(new, 7 stages)_ |
| 3   | New read-only discovery agent: scans an unfamiliar repo, emits ✅/❌/⚠️ checklist mapping each missing artifact to the right skill/playbook. Forbidden from any write/edit/container op. | `.github/agents/setup-discovery.md` _(new)_ |
| 4   | `@copilot-setup-maintainer` Workflow 13 — codebase evolver pass: detects stale ADR statuses, missing skill files for recurring patterns, missing eval queries, missing memory-promotion entries; surfaces findings, does NOT auto-fix | `.github/agents/copilot-setup-maintainer.md` |
| 5   | `doc-suggestions.instructions.md` +4 trigger sections: new skill needed (3+ recurring corrections), new eval query needed (audit shows uncovered file), new memory entry needed (correction repeats 2nd time), new ADR needed (pattern shipped without one) — all suggest-only | `.github/instructions/doc-suggestions.instructions.md` |
| 6   | Hook cleanup: deleted stale `posttooluse-chain.ps1` (relic, never wired). `posttooluse-chain.sh` KEPT — documented bash fallback for SSH/headless Linux + macOS (Session 31 changelog, ADR-0029 Amendment 001, `docs/rag/auto-cache-hook.md`) | `.github/hooks/posttooluse-chain.ps1` _(deleted)_ |
| 7   | docs-index sync: +9 new skill rows (D1-D3, E1-E5, B10), +3 playbook rows, +setup-discovery agent row | `.github/instructions/docs-index.instructions.md` |
| 8   | sln drift cleared: +ADR-0027 / ADR-0028 parent folders + amendments, +`docs/roadmap/rag-remote-multitenant.md`, +new skill folders + playbook files in Copilot/docs solution folders | `ECommerceApp.sln` |

**Counts**: skills 24 → 33, agents 8 → 9, playbooks 0 → 3 (hub + 2). Instructions unchanged in count (Workflow 13 extends existing maintainer agent; doc-suggestions extends existing instruction file).

**Not changed (deliberate, per Sprint 3+4 scope)**:

- ADR-0028 Amendment 4 / roadmap Phase 3 P3-1..P3-8 — too large for this session. D2/E1/E2 skills document the gap via KNOWN GAP blockquotes; standalone-stack projects are unaffected.
- `posttooluse-chain.sh` retained — documented bash fallback for non-Node hosts.
- VS Code default PowerShell shell setting kept — user confirmed they accept the existing host behavior.

**Refs**: branch `feat/sprint1-rag-quick-wins-skills-plan`, parent commit `07b0bcfe`, [handoff brief](../docs/reports/sprint3-sprint4-requirements-brief.md), [ADR-0029](../docs/adr/0029/0029-context-mode-mcp-sandbox.md), [ADR-0028 Amendment 4](../docs/adr/0028/amendments/0028-004-per-collection-config-gap.md).

---

### Session 31 — Sprint 1 RAG quick wins + 3 context-mode sandbox skills (2026-05-28)

First commit of the cross-project RAG/context-mode reusability initiative (full plan: [docs/reports/rag-context-mode-skills-plan-2026-05-28.md](../docs/reports/rag-context-mode-skills-plan-2026-05-28.md)). Phase 0+1 = query-time RAG config wins, Phase 2 = context-mode GA-blocker skills, Phase 3 = this changelog + docs-index sync.

| #   | Change | Files affected |
| --- | ------ | -------------- |
| 1   | +6 named queries (ADR-0026/0027/0028/0029, context-mode-bootstrap-flow, rag-caching-strategy) | `tools/rag/queries.yaml` |
| 2   | +8 PL/DE glossary entries (context-mode, sandbox, AdGuard, refresh-token, IAM, host-side hook, auto-cache, atomic switch) | `tools/rag/multilingual-glossary.yaml` |
| 3   | Initial audit report (EN): 4 RAG configs + 6 existing skills + gap list (10 RAG + 5 context-mode missing) + `docs/rag/**` exclude blocker options A/B/C | `docs/reports/rag-context-mode-audit-2026-05-28.md` _(new)_ |
| 4   | Plan v3 (EN): 32-skill catalogue (A1-E5) + 3-stage bootstrap chain (context-mode-bootstrap → rag-bootstrap → existing mcp-first-routing-migration-playbook) + 2 new agents `setup-discovery`/`setup-evolver` + 7-step delivery schedule | `docs/reports/rag-context-mode-skills-plan-2026-05-28.md` _(new)_ |
| 5   | +3 context-mode sandbox skills: `ctx-sandbox-bootstrap-verify` (8 runtime checks), `ctx-doctor-playbook` (message → fix map, 5 sections), `ctx-hardening-audit` (22 ADR-0029 conformance items pre-merge gate) | `.github/skills/ctx-sandbox-bootstrap-verify/SKILL.md` _(new)_, `.github/skills/ctx-doctor-playbook/SKILL.md` _(new)_, `.github/skills/ctx-hardening-audit/SKILL.md` _(new)_ |
| 6   | Routing table: new "context-mode sandbox skills" section added under RAG maintenance skills | `.github/instructions/docs-index.instructions.md` |
| 7   | Refreshed auto-generated index stats from prior ingest runs | `docs/rag/index-stats.md`, `docs/rag/index-stats-dotnet.md` |

**Counts**: skills 17 → 20. Instructions, agents, prompts, ADRs unchanged.

**Not changed (deliberate)**:

- `metadata-rules.yaml` / `rag-config.yaml` — `docs/rag/**` exclude decision (option A/B/C) deferred to user; skipping Sprint 1 split avoids forcing a full reingest before decision is made.
- HTTP MCP servers not restarted in this commit — query-time configs (queries.yaml, glossary) require restart for HTTP variants only; stdio variants reload per VS Code session.

**Sln drift fixed in this commit**: 3 new skill projects added under `skills` solution folder + 2 new reports added under `reports` SolutionItems (close-out audit Session 31 caught the missed sync; corrected immediately).

**Pre-existing drift (still open, flagged in Session 24+27)**: ADR-0027 and ADR-0028 not in `.sln`. Two .NET RAG indexer bugs (ADR-0028 `main_file` + amendment count) tracked in `/memories/repo/rag-mcp-anomalies.md` — Sprint 2 work.

**Refs**: `docs/reports/rag-context-mode-skills-plan-2026-05-28.md`, ADR-0029, `docs/adr/0029/0029-context-mode-mcp-sandbox.md` §Conformance checklist.

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
