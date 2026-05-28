# RAG + context-mode — Skills production & playbook evolution plan (2026-05-28)

> Decision document. Consolidates 2 prior audits + a full playbook scan.
> The user decides what we implement and in what order.

---

## Design assumptions (from session discussion)

1. **A skill is a unit of work** invoked by an agent. The agent delegates → the skill executes.
2. Selected skills are **dual-mode**:
   - **Mode A — IMPROVE**: optimise/extend the existing setup after new functionality appears in the project.
   - **Mode B — BOOTSTRAP**: stand up the functionality from zero in another project (after a playbook interview).
3. **Playbooks are intelligent**: they interview the user (HITL via `vscode_askQuestions` with custom multiline input), scan the project + its docs, and can say **"you do not need this"** or **"build the docs first"**.
4. **RAG and context-mode have different usefulness profiles**:
   - context-mode: nearly every project benefits (cost cuts, larger context, sandboxed exec) — the playbook **recommends by default**.
   - RAG: requires investment (docs, collections, calibration, Qdrant hosting) — the playbook **runs a discovery interview** to decide whether it makes sense at all.

---

## Part 1 — Full skills catalogue (20+ → can grow further)

Legend: 🅰️ = Improve only · 🅱️ = Bootstrap only · 🅰️🅱️ = dual-mode · ⚙️ = pure ops/debug

### A. RAG — existing improvement & tuning skills (**will be extended**)

| # | Skill | Mode | What it does |
|---|---|---|---|
| A1 | `diagnose-rag` _(existing)_ | ⚙️ | Decision tree for 7 triage paths: MCP fails to start, tools visible but errors, wrong doc at #1, PL/DE fails, low scores, .NET DLL lock, stale index. **Extension**: add a "full reset" section (purge collection + force-full ingest) and a symptom map for the 6 variants (stdio-local/docker/http × Python/.NET). |
| A2 | `expand-rag-glossary` _(existing)_ | 🅰️ | Add PL/DE synonyms to `multilingual-glossary.yaml`. Query-time only. **Extension**: add "corpus import" (auto-detect terms from PL/DE titles in docs/), not just user-driven. |
| A3 | `generate-eval-questions` _(existing)_ | 🅰️ | Auto-generate eval queries for new docs. **Extension**: cross-check that proposed queries actually return the target file at #1 (test loop). |
| A4 | `generate-rag-rules` _(existing)_ | 🅰️🅱️ | **Mode A**: update `metadata-rules.yaml` + `queries.yaml` after adding a folder/type. **Mode B**: scan a new project → generate initial rules from scratch based on `docs/` structure. |
| A5 | `rag-with-memory` _(existing)_ | ⚙️ | L1/L2/L3 cache for RAG → context-mode FTS5. Already well documented. |
| A6 | `tune-rag-weights` _(existing)_ | 🅰️ | Adjust `ranking.weights` in `rag-config.yaml`. **Extension**: a script that measures per-query score before/after for A/B comparison. |

### B. RAG — new skills (gaps)

| # | Skill | Mode | What it does |
|---|---|---|---|
| B1 | `rag-parity-audit` | ⚙️ | Compare Python vs .NET outputs on a test query set: `list_adrs` (titles, main_file, amendment counts), `query_docs` (top-k, order, scores), `read_docs`, error envelopes. Generates a diff. Today it would immediately catch the ADR-0028 main_file bug. |
| B2 | `rag-cli-ingest-fix` | ⚙️ | Fix the broken `--remote` CLI in both stacks (per-file POST → ZIP `/batch`). Tracked blocker. |
| B3 | `rag-collection-ops` | 🅰️ | Qdrant management: rename / archive / merge / clone / snapshot / restore / orphan audit. Multi-tenant safety. |
| B4 | `rag-variant-migration` | 🅰️ | Switch a team between Python ↔ .NET: embedder compatibility (different tokenizers → re-embed), collection migration, parity validation, rollback. |
| B5 | `rag-validation-hardening` | ⚙️ | Close the 9 gaps from `/memories/repo/rag-mcp-anomalies.md`: size cap, top_k clamp, collection-name length, type validation, ZIP path traversal, MaxRequestBodySize, JSON-RPC envelopes. |
| B6 | `rag-tool-endpoint-dev` | 🅰️🅱️ | **A**: add a new `query_*` tool to both stacks simultaneously. **B**: in a new project, pick which tools to expose at all (minimal: 2; full: 5+). |
| B7 | `rag-performance-tuning` | 🅰️ | Profile slow queries, Qdrant HNSW params (`ef_construct`, `m`), payload filtering, embedding latency (CPU vs GPU), ingest memory. Output: benchmark + recommendations. |
| B8 | `rag-embedder-upgrade` | 🅰️ | Embedder model upgrade plan: smoke eval on eval queries, full re-embed, recall@1 validation, rollback. |
| B9 | `rag-empty-result-deep-dive` | ⚙️ | When the 2-step retry from mcp-routing didn't help: a diagnostic tree (chunk in Qdrant? embedder mapping? weight? glossary? threshold?). Concrete commands. |
| B10 | `rag-bootstrap-from-zero` | 🅱️ | **NEW KEY SKILL**: stand RAG up in another project from zero — pick variant (Python/.NET, stdio/http, local/docker), generate initial `rag-config.yaml`, `metadata-rules.yaml`, `queries.yaml`, `multilingual-glossary.yaml` (based on project scan), Qdrant setup, ingest, smoke test. Pre-req: playbook confirmed RAG makes sense. |
| B11 | `rag-coverage-eval-audit` | 🅰️ | Identifies "orphan files" — docs without a named query. Suggests queries from H1/H2 + tests them. |
| B12 | `rag-uninstall` | ⚙️ | Safely uninstall RAG from a project if it turned out not to fit: drop collection, remove configs, archive index, update routing instructions. |

### C. context-mode — from zero (10 new)

| # | Skill | Mode | What it does |
|---|---|---|---|
| C1 | `ctx-sandbox-bootstrap-verify` | 🅱️ | After `context-mode-bootstrap.ps1`, verifies 6 hardening flags, DNS through AdGuard, network-monitor.js, FTS5 DB writeable, `ctx_doctor` green, ports on 127.0.0.1, AdGuard `allowed_clients`. Each check = pass/fail + fix link. |
| C2 | `ctx-adguard-allowlist-onboard` | 🅰️ | Procedure to add a domain: check why blocked (logs), identify patterns, validate, add to `team-whitelist.txt`, reload, smoke test, PR template with security rationale. |
| C3 | `ctx-doctor-playbook` | ⚙️ | Map: every known message from `ctx_doctor()` → cause → fix. Concrete commands. |
| C4 | `ctx-hook-debugging` | ⚙️ | Debug PreToolUse / PostToolUse / PreCompact: logs, env vars, JSON syntax, single-hook execution offline. |
| C5 | `ctx-network-alerts-forensics` | ⚙️ | Parser for `.ctx-network-alerts.log` + AdGuard query log, per-domain aggregation, security/compliance report. |
| C6 | `ctx-performance-troubleshooting` | 🅰️ | SQLite VACUUM/ANALYZE, FTS5 ranking (Porter vs trigram), memory pressure → compaction, EXPLAIN QUERY PLAN. |
| C7 | `ctx-tool-integration` | 🅰️🅱️ | **A**: add a new `ctx_*` tool. **B**: in a new project, pick the tool set (minimal vs full). |
| C8 | `ctx-session-export-import` | 🅰️ | SQLite backup/restore: `cp` from volume, migration between versions (schema upgrade?), restore. |
| C9 | `ctx-hardening-audit` | ⚙️ | Full checklist of 22 items from ADR-0029 §Conformance. Programmatic verification + compliance report. |
| C10 | `ctx-upgrade-procedure` | 🅰️ | Safe upgrade of `CONTEXT_MODE_TAG`: pre-flight (hooks/FTS5), backup, build, smoke test, rollback, `ctx_doctor` validation. |

### D. context-mode — bootstrap from zero (new)

| # | Skill | Mode | What it does |
|---|---|---|---|
| D1 | `ctx-bootstrap-from-zero` | 🅱️ | **NEW KEY SKILL**: stand context-mode up in another project from zero — pick runtimes (Node default, options JS/Shell vs full set), generate Dockerfile-context-mode + docker-compose + AdGuard configs (with a default hardened profile), first initialisation, smoke test. |
| D2 | `ctx-hooks-bootstrap` | 🅱️ | In a new project: set up `.github/hooks/` (auto-cache, PreToolUse, network-monitor), pick hooks that match the project (e.g., does it have RAG?), test every hook offline. |
| D3 | `ctx-mcp-wiring-bootstrap` | 🅱️ | First configuration of `.vscode/mcp.json` for context-mode in a new project, verify VS Code Chat sees it, smoke test all 11 tools. |

### E. Cross-cutting — shared

| # | Skill | Mode | What it does |
|---|---|---|---|
| E1 | `setup-discovery-interview` | 🅱️ | **KEY**: the skill that conducts the interview (via `vscode_askQuestions` with multiline custom input): team size, language(s), do docs exist, what use case, multi-tenant, CI/CD, offline-first, GPU available, etc. Output: a stack recommendation (RAG yes/no, which variant, context-mode yes/no, hooks). Invoked by a playbook. |
| E2 | `project-doc-scanner` | 🅱️ | Scans the project (filesystem + git history + README + package.json/csproj) and extracts: stack, folder structure, presence of ADRs, presence of docs, presence of instructions, language(s), framework(s), test framework. Feed for `setup-discovery-interview`. |
| E3 | `setup-bootstrap-validator` | 🅱️ | After bootstrapping the full stack (RAG + context-mode + hooks) — orchestrates all smoke tests (`test-mcp-handshake.ps1`, `test-ctx-doctor.ps1`, `test-ctx-fetch.ps1`, `query_docs` smoke, parity check). Single report: setup complete ✅ or needs fixes 🔴 with a fix list. |
| E4 | `docs-first-bootstrap` | 🅱️ | When the interview discovers the project has no documentation at all — first generate a minimal `docs/README.md` + `docs/architecture/` + ADR template. Only then does RAG make sense. |
| E5 | `copilot-instructions-bootstrap` | 🅱️ | In a new project: generate `.github/copilot-instructions.md` + `instructions/*.instructions.md` + `prompts/*.prompt.md` adapted to the detected stack (.NET / Node / Python / Mixed). |

**Total: 32 skills** (6 existing + 12 new RAG + 13 new context-mode/cross-cutting + 1 uninstall).

We can add more if needed — the user gave green light on "more is fine".

---

## Part 2 — Playbook evolution (HITL + intelligent interview) — v3

### What already exists — full map

| File | Type | Scope | Action |
|---|---|---|---|
| `.github/prompts/rag-sync.prompt.md` | playbook | RAG **improve only** (post-setup maintenance) | Light update (add B1 parity, B11 coverage) |
| `.github/prompts/{bc-analysis, bc-implementation, batched-tasks, mcp-routing-eval, pr-review, refactor, flow-analysis}.prompt.md` | playbooks | Other categories | Unchanged |
| `docs/rag/mcp-first-routing-migration-playbook.md` | playbook | **Routing migration** — agent reads MCP instead of files. Explicit: "You can only migrate once the RAG side is actually working" | **KEEP + light update** (chain stage 3) |
| `docs/rag/SETUP-GUIDE.md` | manual guide | Step-by-step RAG setup | Update: add discovery section (Do you need RAG?) |
| `docs/getting-started-context-mode.md` | manual guide | Manual context-mode setup | Update: add short discovery |
| `scripts/context-mode-bootstrap.ps1` | script | Idempotent context-mode bootstrap | Unchanged |
| Agents: `planner`, `implementer`, `verifier`, `bc-switch`, `copilot-setup-maintainer`, `adr-generator`, `code-reviewer`, `pr-commit` | Full pipeline orchestration | Unchanged |

### Bootstrap chain (3 stages, recycling existing)

```
[Stage 1] context-mode-bootstrap.prompt.md (NEW)         — foundation; almost everyone benefits
   ↓
[Stage 2] rag-bootstrap.prompt.md (NEW)                  — interview: does RAG make sense? + bootstrap if yes
   ↓
[Stage 3] mcp-first-routing-migration-playbook.md (UPDATE) — switch routing in instructions (already exists!)
```

### Proposed new playbooks / agents

#### Playbook 1 — `context-mode-bootstrap.prompt.md` (NEW, stage 1)
Mirrors `docs/getting-started-context-mode.md` + `scripts/context-mode-bootstrap.ps1`. Orchestrates E2 scanner → E1 interview (shorter, almost always YES) → D1+D2+D3 → C1 verify → E3 validator.

#### Playbook 2 — `rag-bootstrap.prompt.md` (NEW, stage 2)
Mirrors `docs/rag/SETUP-GUIDE.md`. Orchestrates E2 scanner → E1 interview → decision (RAG yes/no — may decline!) → B10 bootstrap → E3 validator.

#### Playbook 3 — `setup-new-project.prompt.md` (OPTIONAL orchestrator)
For one-command convenience — runs the 3 stages in sequence (context-mode + RAG + routing migration). Skip if user prefers manual control.

**HITL flow with break-points (example for `rag-bootstrap.prompt.md` — the most extensive one)**:
1. **Project auto-scan** (E2): detects stack, languages, presence of docs/ADRs, framework, CI.
2. **HITL interview** (E1): series of `vscode_askQuestions` with custom multiline. Questions include:
   - Team size? (1 / 2–5 / 6–20 / 20+) + custom
   - Do you use multiple languages in the docs? (EN only / PL+EN / DE+EN / other) + custom
   - Do you have existing documentation? (yes / no / partial) + custom (where does it live?)
   - Main Copilot use case? (knowledge lookup / code gen / debug / architecture decisions / everything) + custom
   - Multi-tenant project? (yes / no) + custom
   - GPU available for embedders? (yes / no / cloud)
   - Offline-first? (yes / no)
   - Does the team have Docker experience? (yes / no / some) + custom
   - Free custom input: "Anything else Copilot should know before recommending?"
3. **Recommendation** (within E1), e.g.:
   - "Solo dev, no docs, short project → SKIP RAG. Enable context-mode only."
   - "Team of 10, lots of docs, multi-language → Python RAG + auto-cache + context-mode. Full pack."
   - "No docs, but you want RAG → first run E4 `docs-first-bootstrap`, then RAG."
   - "CI only, no local Docker → context-mode not recommended; choose RAG hosted in CI only."
4. **HITL Checkpoint 1**: user sees the recommendation, accepts / modifies / rejects (custom input).
5. **Execute**: orchestrates the chosen skills (B10, D1, D2, D3, E5).
6. **Validate** (E3): smoke test everything.
7. **HITL Checkpoint 2**: report of what works / what doesn't. Decision: continue / iterate / rollback.

#### Agent 1 — `setup-discovery` (NEW)
A lightweight agent that invokes the appropriate playbook (`context-mode-bootstrap`, `rag-bootstrap`, or the `setup-new-project` orchestrator) on trigger phrases: "setup new project", "init RAG", "init context-mode", "bootstrap copilot stack".

#### Agent 2 — `setup-evolver` (NEW)
Agent for MODE A (improve the existing setup). Detects "new functionality landed, I want to optimise":
- Scans what changed (git diff since the last run)
- Checks whether configs are up to date (queries.yaml, glossary, metadata-rules, hooks)
- Suggests which `improve` skills to run (A4, A6, B11, C2, C6, etc.)
- HITL: user approves the list
- Runs them in sequence, ending with `copilot-setup-maintainer`

### Changes to existing playbooks

| Playbook | Change |
|---|---|
| `rag-sync.prompt.md` | Add a section "run `rag-parity-audit` (B1) as a pre-flight check before ingest" + "run `rag-coverage-eval-audit` (B11) as post-flight" |
| `context-mode-integration.md` (roadmap) | After completing Phase 3, append Phase 6 = "build the C1+C3+C9 pack as a GA blocker" |
| `mcp-routing.instructions.md` | After context-mode skills exist, append a section "If a problem with context-mode → invoke skill C3 / C4 / C5 by symptom" |

---

## Part 3 — Change report (what we need to change, in execution order)

### Step 0 — Quick wins / configs (~30 min, zero risk)

| File | Change |
|---|---|
| `tools/rag/queries.yaml` | Add 4 ADR queries (0026, 0027, 0028, 0029) + 2 operational (context-mode-bootstrap, rag-caching-strategy) |
| `tools/rag/multilingual-glossary.yaml` | Add 8 PL/DE entries (context-mode, sandbox, AdGuard, refresh-token, IAM, host-side, auto-cache, atomic switch) |
| `tools/rag/metadata-rules.yaml` | Split `rag_meta` → `rag_meta` (0.70) + `rag_guide` (0.85–0.90) for operational guides in `docs/rag/` (blocked by `docs/rag/**` exclude — see audit Part 5) |
| `tools/rag/rag-config.yaml` | Add weights for the new `rag_guide` kind (same blocker) |
| **After edit**: incremental ingest for Python + .NET | `python tools/rag/ingest.py` + .NET ingest |

### Step 1 — context-mode GA-blocker pack (~1 day)

Create skills:
1. C1 `ctx-sandbox-bootstrap-verify`
2. C3 `ctx-doctor-playbook`
3. C9 `ctx-hardening-audit`

Together: they secure rollout to the team. Without them, silent-failure risk = high.

### Step 2 — RAG parity + indexer fixes pack (~3–4 days)

- B1 `rag-parity-audit` (~2–3 days) → catches all 7+ bugs + current ADR-0028 / amendment count
- Fix .NET indexer (2 concrete bugs): main_file (sort by filename instead of `<id>-*.md` prefix), amendment count (count top-level docs instead of chunks)

### Step 3 — Debug & ops pack (~4–5 days, can run in parallel)

- C4 `ctx-hook-debugging`
- C5 `ctx-network-alerts-forensics`
- B9 `rag-empty-result-deep-dive`
- Update existing: A1 `diagnose-rag` (add "full reset" + 6-variant symptom map)

### Step 4 — Bootstrap-from-zero pack (~1 week, biggest writing effort)

- E2 `project-doc-scanner` (foundation)
- E1 `setup-discovery-interview` (hardest — needs good interview with fallbacks)
- E5 `copilot-instructions-bootstrap`
- E4 `docs-first-bootstrap`
- B10 `rag-bootstrap-from-zero`
- D1 `ctx-bootstrap-from-zero`
- D2 `ctx-hooks-bootstrap`
- D3 `ctx-mcp-wiring-bootstrap`
- E3 `setup-bootstrap-validator`
- Playbook 1 `context-mode-bootstrap.prompt.md`
- Playbook 2 `rag-bootstrap.prompt.md`
- Playbook 3 (optional) `setup-new-project.prompt.md`
- Agent 1 `setup-discovery.md`

### Step 5 — Improve-mode evolver pack (~2–3 days)

- Agent 2 `setup-evolver.md`
- Update existing playbooks (`rag-sync`, etc.)

### Step 6 — Full operational pack (~1 week, lower priority)

- B2 `rag-cli-ingest-fix` (known blocker, but a workaround exists)
- B3 `rag-collection-ops`
- B4 `rag-variant-migration`
- B5 `rag-validation-hardening` (security — may get priority if multi-tenant ships)
- B6 `rag-tool-endpoint-dev`
- B7 `rag-performance-tuning`
- B8 `rag-embedder-upgrade`
- B12 `rag-uninstall`
- C2 `ctx-adguard-allowlist-onboard`
- C6 `ctx-performance-troubleshooting`
- C7 `ctx-tool-integration`
- C8 `ctx-session-export-import`
- C10 `ctx-upgrade-procedure`

### Step 7 — Extensions to existing skills (~1 day)

- A2 `expand-rag-glossary`: add corpus auto-import
- A3 `generate-eval-questions`: add test loop
- A4 `generate-rag-rules`: add bootstrap mode (B)
- A6 `tune-rag-weights`: add A/B benchmark script
- B11 `rag-coverage-eval-audit` (new but related)

---

## Suggested sprint order (Copilot recommendation)

**Sprint 1 (~2 days)**: Step 0 + Step 1. Quick wins + GA-blocker for context-mode.
**Sprint 2 (~1 week)**: Step 2 + Step 3. Parity audit, indexer fixes, debug skills.
**Sprint 3 (~1.5 weeks)**: Step 4. Bootstrap-from-zero — large, transformative.
**Sprint 4 (~2–3 days)**: Step 5 + Step 7. Evolver + extensions.
**Sprint 5+ (rolling)**: Step 6. Full operational pack — drop-in skills, can be delivered one at a time.

**Total effort**: ~3–4 weeks of focused work for the full plan. Sprint 1 quick wins deliver value today.

---

_Generated 2026-05-28. Infra status: 6/6 RAG variants ✅, fresh ingest, Qdrant/AdGuard/context-mode healthy._
