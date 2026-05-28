# Sprint 3 + Sprint 4 — Requirements brief

> **Purpose**: Hand off Sprint 3 (cross-project setup skills + playbooks + @setup-discovery agent) and Sprint 4 (maintainer Workflow 13 + doc-suggestions extensions) to a fresh session with full context. **Quality bar: full-fledged artifacts (200–300 lines per skill), commit-ready.**

> **Context for the next session**: Sprint 1 done (committed on `feat/sprint1-rag-quick-wins-skills-plan`, 3 commits, not pushed). Sprint 2 done (single squash-ready commit prepared at end of this session). Sprint 3 + 4 are an additive layer; no Sprint 2 file should be modified.

---

## 0. Branch state & ground rules

- **Branch**: `feat/sprint1-rag-quick-wins-skills-plan` (continue on the same branch).
- **Commit policy**: one final commit at the end of Sprint 4 covering all Sprint 3 + 4 work, message style `feat(setup): cross-project bootstrap skills + maintainer evolution (Sprint 3+4)`.
- **Quality bar**: each skill must include — frontmatter, "When to use" + "When NOT to use", numbered Steps, Common mistakes (≥3), worked example or invocation snippet, Related skills/docs section. Target 200–300 lines.
- **Reports must be in English** (user requirement).
- **MCP routing**: pre-edit RAG check on every doc/file (`query_docs("<area>")`) per `.github/instructions/mcp-routing.instructions.md`. Use the L3 hook — every RAG call auto-caches.
- **End every response with `vscode_askQuestions` + custom multiline freeform field** (user requirement).
- **`@copilot-setup-maintainer` mandatory at session end** — Workflow 11 + 7 minimum. Possibly Workflow 6 (full audit) given the volume of new files.

---

## 1. Sprint 3 — scope

### Group D: ctx-bootstrap skills (3 skills, must apply to ANY new project, not just this repo)

These skills teach an agent how to bootstrap a context-mode-backed sandbox for an arbitrary project — network, storage, runtime allowlists. They are referenced by the higher-level E1 skill.

#### D1 — `.github/skills/ctx-bootstrap-network/SKILL.md`

- **What**: how to define the AdGuard DNS allowlist for a new project — which domains are essential (Qdrant, embedder downloads, GitHub for ADR refs), which are forbidden (telemetry endpoints, package registries unless explicitly allowed).
- **Sources to RAG-cache before writing**: `docs/adr/0029/0029-context-mode-mcp-sandbox.md`, `docker/adguard/`, `scripts/adguard/domain-policy.ps1`, `docs/getting-started-context-mode.md`.
- **Frontmatter argument-hint**: `"<project-name> <domain-allowlist-yaml-path>"`.
- **Worked example**: walk through adding "MyOtherProject" with its own Qdrant + a HuggingFace embedder download. Show the allowlist YAML diff.
- **Common mistakes**: forgetting CDN domains for embedder downloads, allowing `*` instead of specific subdomains, not reloading AdGuard after edit.

#### D2 — `.github/skills/ctx-bootstrap-storage/SKILL.md`

- **What**: how to provision per-project Qdrant collection + the host-mounted FTS5 SQLite file for context-mode's `ctx_index`/`ctx_search`.
- **Sources to RAG-cache**: `docs/adr/0028/0028-remote-multitenant-rag-ingest.md` + Amendment 004 (per-collection config gap — **flag as not-yet-fixed**), `docker-compose.yaml` storage sections, `Dockerfile-context-mode`.
- **Frontmatter argument-hint**: `"<project-name> [--collection-name=<name>]"`.
- **Worked example**: provision collection `my_project_docs` and SQLite path `/data/my_project/ctx.db`. Show the env vars / volume mounts.
- **Common mistakes**: re-using a collection name across projects (loses isolation per ADR-0028), forgetting bind-mount on host so data is lost on container recreation, missing `WORKDIR` for the SQLite file.

#### D3 — `.github/skills/ctx-bootstrap-runtimes/SKILL.md`

- **What**: how to verify and (if missing) install language runtimes inside the context-mode sandbox. Currently only `javascript` (Node) and `shell` (POSIX sh) ship. Adding Python, .NET, Go, etc. requires Dockerfile edits.
- **Sources to RAG-cache**: `Dockerfile-context-mode`, `.github/instructions/mcp-routing.instructions.md` (the `ctx_doctor` runtime quirk note), prior context-mode integration roadmap entries.
- **Frontmatter argument-hint**: `"<language> [--install]"`.
- **Worked example**: enable Python in the sandbox — Dockerfile diff + verification with `ctx_doctor`.
- **Common mistakes**: assuming the schema enum means the runtime is shipped (it doesn't), `ctx_execute("python", ...)` returns runtime-not-available error not 404, installing runtime in `/usr/local/bin` instead of `/app/runtimes/` (sandbox path).

---

### Group E: cross-project setup skills (5 skills — these are the headline Sprint 3 deliverables)

Each E skill assumes a brand-new git repository with no prior RAG/context-mode setup. They walk an agent through making the project work end-to-end.

#### E1 — `.github/skills/setup-rag-new-project/SKILL.md`

- **What**: bring up RAG (Python + .NET HTTP + Qdrant) for a NEW project. Cover: `rag-config.yaml` template, `metadata-rules.yaml` template per project (catch-all rules for `docs/`, `.github/context/`), embedder model choice (default MiniLM 384-dim), Docker Compose stanza, first ingest, smoke test.
- **Sources to RAG-cache**: `docs/rag/rag-architecture.md`, `docs/adr/0027`, `docs/adr/0028`, `tools/rag/rag-config.yaml`, `tools/rag/metadata-rules.yaml`, `docker-compose.yaml`.
- **Argument-hint**: `"<project-name> [--embedder=<model>] [--language=python|dotnet|both]"`.
- **Worked example**: end-to-end for "AcmeApp" — copy templates, edit, ingest, query.
- **Common mistakes**: skipping `metadata-rules.yaml` (causes all chunks to default to `doc_kind=other`), pointing both servers at the same collection (causes write contention), forgetting `qdrant` service in compose.

#### E2 — `.github/skills/setup-context-mode-new-project/SKILL.md`

- **What**: bring up context-mode sandbox for a new project. Includes wiring up the L3 RAG auto-cache hook.
- **Sources to RAG-cache**: `docs/adr/0029`, `Dockerfile-context-mode`, `docker/context-mode/`, `.github/hooks/auto-cache.mjs`, `.github/copilot/mcp.json`, `.vscode/mcp.json`.
- **Argument-hint**: `"<project-name> [--with-auto-cache]"`.
- **Worked example**: complete bootstrap including the AdGuard policy registration + MCP client config.
- **Common mistakes**: skipping AdGuard (sandbox can't reach Qdrant), using stdio transport in CI (works locally only), forgetting to mount the FTS5 SQLite file.

#### E3 — `.github/skills/setup-adguard-policy/SKILL.md`

- **What**: stand up AdGuard Home as the DNS-level egress firewall for a new project.
- **Sources to RAG-cache**: `docker/adguard/`, `scripts/adguard/domain-policy.ps1`, `docs/adr/0029` §egress-policy.
- **Argument-hint**: `"<project-name> [--strict|--permissive]"`.
- **Worked example**: full compose stanza + initial filter file + reload procedure.
- **Common mistakes**: using `0.0.0.0` block instead of NXDOMAIN (some clients retry forever on connection-refused), filter file path mismatch between container and host bind-mount, forgetting upstream resolver config.

#### E4 — `.github/skills/setup-mcp-clients/SKILL.md`

- **What**: configure MCP clients for a new project — VS Code (`.vscode/mcp.json`), GitHub Copilot Web (`.github/copilot/mcp.json`), Visual Studio (17.14+, JSON shape differs).
- **Sources to RAG-cache**: `.vscode/mcp.json`, `.github/copilot/mcp.json`, ADR-0028 (collection naming).
- **Argument-hint**: `"<client> <project-name>"`.
- **Worked example**: side-by-side comparison of all 3 client schemas for the same RAG + context-mode endpoints.
- **Common mistakes**: mixing stdio + HTTP in the same client config, hardcoding `localhost:3001` (use service name in compose context), forgetting `mcp-session-id` header in custom HTTP clients.

#### E5 — `.github/skills/setup-auto-cache-hook/SKILL.md`

- **What**: install and wire up the L3 RAG-to-context-mode auto-cache hook for a NEW project.
- **Sources to RAG-cache**: `.github/hooks/auto-cache.mjs`, `docs/rag/auto-cache-hook.md`, ADR-0029 Amendment 1, `mcp-routing.instructions.md` L3 section.
- **Argument-hint**: `"<project-name> [--debug]"`.
- **Worked example**: hook drop-in + first-run verification (use `ctx_search(source="rag-auto-")`).
- **Common mistakes**: hook firing but `ctx_index` failing silently (check `AUTO_CACHE_DEBUG=1`), wrong source label prefix (must be `rag-auto-` for the recall convention to work), running the hook without context-mode being up (silent no-op then surprise).

---

### Group B continued: B10 — `.github/skills/rag-eval-coverage/SKILL.md`

- **What**: check which files in the corpus have NO named eval query covering them, and propose new queries.
- **Sources to RAG-cache**: `tools/rag/queries.yaml`, the parity audit report, `.github/skills/generate-eval-questions/SKILL.md`.
- **Argument-hint**: `"<glob>"` (e.g. `"docs/adr/00*/0*-*.md"`).
- **Worked example**: walk through finding uncovered ADRs and writing a Q for one of them.
- **Common mistakes**: writing too-specific queries (only match one chunk), writing too-vague queries (top-1 is always agent-decisions.md), forgetting to add the query to `compare_queries.py` for parity tracking.

---

### Group P: playbooks (2 playbooks — multi-page docs, not skills)

#### P1 — `docs/playbooks/context-mode-bootstrap.md`

A long-form (1000–1500 lines) walkthrough invoking D1+D2+D3+E2+E3+E5 in sequence for a hypothetical new project. Each section ends with "verification" so the agent can checkpoint. Cross-references all relevant ADRs.

#### P2 — `docs/playbooks/rag-bootstrap.md`

Same shape for E1 + E4 + (optional) hook into existing context-mode. Includes troubleshooting flowchart for common first-run failures.

---

### New agent — `.github/agents/setup-discovery.md`

- **Purpose**: scan a NEW git repository (different from this one) and report what's already set up vs what needs bootstrapping. Outputs a checklist.
- **Trigger phrases**: "discover setup", "what setup exists", "audit project bootstrap".
- **Tools allowed**: read-only file inspection, `ctx_execute("sh", ...)` for filesystem scans, RAG (this repo's RAG, to compare against canonical patterns).
- **Tools forbidden**: any write/edit/delete, any commit, any container operations.
- **Output shape**: markdown checklist with ✅ / ❌ / ⚠️ per artifact (`docker-compose.yaml`, `.vscode/mcp.json`, `tools/rag/`, etc.).

---

### SETUP-GUIDE updates

If a `SETUP-GUIDE.md` (or similar) exists at repo root, append a "For new projects" section pointing at the E1–E5 skills + P1/P2 playbooks. If no such file exists, **do not create one** — leave a TODO in the maintainer cascade note and the maintainer agent will decide where it belongs.

---

## 2. Sprint 4 — scope

### W13 — Add Workflow 13 to `@copilot-setup-maintainer`

- **What**: a new workflow in the maintainer agent that does a "codebase evolver pass" — scans for: stale ADR statuses, missing skill files for new patterns, missing eval queries, missing memory entries for recurring corrections. Outputs a maintainer report.
- **Cascade**: Workflow 13 calls Workflow 7 (changelog) after producing the report.
- **Source**: `.github/agents/copilot-setup-maintainer.md` (read current workflows 1–12 to match style).
- **Quality bar**: same as existing workflows — numbered steps, exit conditions, what-NOT-to-do.

### Doc-suggestions extensions — `.github/instructions/doc-suggestions.instructions.md`

Add triggers (suggest-only, never auto-apply) for:

- A new skill probably needed when: 3+ similar manual fixes have been done in recent sessions on the same area, AND no skill currently exists.
- A new eval query probably needed when: a parity audit shows a corpus file consistently outside top-5.
- A new memory entry probably needed when: a correction in `agent-decisions.md` references the SAME mistake twice — promote.
- A new ADR probably needed when: a new architectural pattern shipped without one.

Each trigger has a "How to suggest" template phrase, identical style to existing triggers in that file.

---

## 3. Verification at end of Sprint 4

Before final commit:

1. **Lint**: every new skill file has frontmatter (`name`, `description`, `argument-hint`), and the file path matches `.github/skills/<name>/SKILL.md`.
2. **Cross-links**: every "Related skills / docs" section uses workspace-relative markdown links (per file-linkification rules) — no inline backticks for file names.
3. **No regressions**: re-run `python tools/rag/compare_queries.py` and confirm the audit metrics are within ±2 top-1 matches of the Sprint 2 baseline (no Sprint-3 doc additions should significantly perturb ranking).
4. **Invoke `@copilot-setup-maintainer`**: Workflow 11 (close-out check) + Workflow 7 (changelog) minimum. If Sprint 3+4 touched 10+ files (likely), Workflow 6 (full audit) is recommended.
5. **Single commit** with the message specified in §0.

---

## 4. Required pre-edit RAG cache list (do this FIRST in the new session)

To avoid wasted token budget, RAG-cache these once at session start:

```text
query_docs("ADR-0027 RAG pipeline architecture")
query_docs("ADR-0028 remote multitenant RAG ingest")          // + Amendment 004
query_docs("ADR-0029 context-mode sandbox DNS firewall")
query_docs("multilingual glossary expansion query preprocessor")
query_docs("docker compose RAG context-mode services")
query_docs("metadata rules doc kind classification")
query_docs("auto-cache hook PostToolUse rag-auto")
read_docs("getting-started-context-mode")
read_docs("how is the L3 auto-cache hook wired")
```

The L3 hook will cache all of these automatically.

---

## 5. Open questions to surface to the human at the start of Sprint 3 session

These are decisions the new session should NOT make alone:

1. **Should Sprint 3 skills target ONLY this repo's stack** (Python+.NET+Qdrant+AdGuard+context-mode) or be **stack-agnostic templates**? Defaults assumed: stack-specific, since otherwise the skills become too abstract.
2. **Should ADR-0028 Amendment 004 (per-collection config gap) be fixed BEFORE** writing E1/E2 (which depend on it working), or **document the gap inside the skills** and ship anyway? Default assumed: document the gap, ship the skills, fix later.
3. **Does the user want `SETUP-GUIDE.md` created** if it doesn't exist? Default assumed: NO — leave the decision to the maintainer.
4. **Squash-commit or multi-commit on Sprint 3+4 work?** User indicated single-commit acceptable, but Sprint 3+4 is ~12 files which is a borderline case.
5. **Are there NEW ADRs needed** for any of the new skills (e.g. a "skill conventions" ADR)? Default assumed: NO — skill conventions already implicit in existing skills.

---

## 6. Reference: Sprint 2 final deliverables (do NOT modify in Sprint 3)

- `tools/rag/queries.yaml` (extended with Q-PRECISE)
- `tools/rag/compare_queries.py` (extended with Q-PRECISE)
- `tools/rag/rag-config.yaml` (R4 reverted — amendments back at 1.20 with explanatory comment)
- `tools/rag-dotnet/rag-config.yaml` (R1+R2 weights)
- `tools/rag-dotnet/multilingual-glossary.yaml` (R3 mirror sync + warning header)
- `docker-compose.yaml` (R3 glossary mounts)
- `tools/rag-dotnet/src/RagTools.Core/QdrantDocumentStore.cs` (indexer bugs partial fix)
- `tools/rag-dotnet/src/RagTools.Core/Query/RagQueryService.cs` (B2 MaxTopK 20→45)
- `tools/rag-dotnet/src/RagTools.Mcp/Tools/RagTools.cs` (B2 comment update)
- `tools/rag/probe_weights.py` (Sprint 2 helper)
- `docs/reports/rag-parity-findings-2026-05-28.md` (§§9, 10, 11)
- `docs/reports/rag-parity-fix-diagnosis-2026-05-28.md` (§10 R4 experiment)
- `docs/reports/rag-parity-audit-2026-05-28.md` (auto-generated)
- `docs/adr/0028/amendments/0028-004-per-collection-config-gap.md` (new)
- `docs/adr/0028/README.md` (linked Amendment 004)
- `docs/roadmap/rag-remote-multitenant.md` (added Phase 3, P3-1..P3-8)
- `.github/skills/rag-reindex-decision/SKILL.md` (new)
- `.github/skills/rag-collection-rebuild/SKILL.md` (new)
- `.github/skills/rag-query-debug/SKILL.md` (new)
- `.github/skills/rag-multilang-test/SKILL.md` (new)
- `/memories/repo/rag-mcp-anomalies.md` entries #8, #9, #10
