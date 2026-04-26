---
applyTo: "**"
---

# Pre-Edit Checklist (mandatory before any edit)

Before proposing or committing changes, perform these steps:

0. **Read agent decisions log** — Open `.github/context/agent-decisions.md` and skim for prior corrections relevant to the area, agent, or skill being used. If a prior entry applies, follow it. This step prevents repeating corrections already given in earlier sessions.
1. **Read context** — Read the entire target file(s) and relevant related files (Controllers, services, repository code, tests).
2. **Read ADRs** — Read ADRs in `docs/adr/` that are directly relevant to the area being changed — not all of them.
3. **Read instructions** — Read the relevant per-stack instructions under `.github/instructions/`.
4. **Search for impact** — Search for usages and migration impact (references, database migrations, API clients) and list affected areas.
5. **Validate locally** — Run `dotnet restore`, `dotnet build`, and `dotnet test` (or explain why not possible).
6. **Include tests** — Include tests for any behavioral change.
7. **Rollback plan** — Include a short rollback/mitigation plan for risky changes.
8. **PR for review** — Open a pull request for review; do not merge without human approval unless explicitly asked.

Document completion of these steps in the PR description.

## Post-edit — append to agent decisions log

If during the task you were corrected on something not yet documented (forgot to read a context file, ignored an instruction, picked the wrong skill, recurring naming/format mistake, etc.), **append a new entry** to `.github/context/agent-decisions.md` using the Variant A format defined in that file. Skip this only when the correction is already covered by an existing entry, ADR, instruction, or anti-pattern.

If the same correction appears for the **2nd time** in the log → **promote** it to a permanent rule (anti-pattern, instruction file, or ADR via `@adr-generator`) and mark the original entries `Status: Promoted → <ref>`.

## Post-edit — invoke @copilot-setup-maintainer (mandatory last step)

After **every task** that changes any file under `.github/` or `docs/` — no matter how small — invoke `@copilot-setup-maintainer` as the final step before closing the task.

**Why this matters for teams:** Every agent, instruction file, ADR row, changelog entry, and `.sln` item must stay in sync for every team member. A single missed sync breaks routing for the next person who picks up the work — they read stale docs, load the wrong ADR, or invoke an agent that no longer matches the pipeline. `@copilot-setup-maintainer` is the shared consistency gate.

**What to run:**

| Changed content                        | Workflows to invoke                        |
| -------------------------------------- | ------------------------------------------ |
| Agent file changed (pipeline agents)   | Workflow 12 → Workflow 7                   |
| Instruction file added/removed/renamed | Workflow 9 → Workflow 7                    |
| ADR added / renamed / archived         | Workflow 1 or 2 → Workflow 7               |
| Roadmap file added                     | Workflow 3 → Workflow 7                    |
| Any `.github/` or `docs/` file changed | Workflow 11 (close-out check) → Workflow 7 |
| Unsure what changed or scope is wide   | Workflow 6 (full audit) → Workflow 7       |

**Minimum always-run:** Workflow 11 + Workflow 7 — even for single-file edits.

> `@copilot-setup-maintainer` owns: `docs-index.instructions.md`, `copilot-instructions.md`, `AGENT-PIPELINE.md`, `COPILOT-SETUP-CHANGELOG.md`, `code-reviewer.md` (cascade only), `ECommerceApp.sln` (Copilot/docs folders).
> It never edits application code, ADR content, or migration files.
