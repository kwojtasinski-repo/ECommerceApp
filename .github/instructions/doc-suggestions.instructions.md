---
applyTo: "**"
---

# During & After Work — Proactive Documentation Suggestions

While implementing or after completing a task, **suggest** (never auto-create) documentation updates when any of these conditions is detected. Always phrase as a proposal — the human decides.

## When to suggest a new ADR

- A significant architectural decision was made during implementation (new pattern, new integration, new cross-BC communication approach).
- An existing ADR's rules were intentionally violated or bent — the deviation needs to be recorded.
- A new bounded context, aggregate, or shared primitive was introduced that doesn't have a design ADR yet.
- A technology or library was added/replaced (e.g., new auth provider, new message broker).
- A decision was debated in the PR or chat and resolved — capture it before the context is lost.

**How to suggest**: _"This change introduces [X]. Consider creating an ADR to record the decision and alternatives considered. Use `@adr-generator` or copy `.github/templates/adr.template.md`."_

## When to suggest updating `docs/architecture/bounded-context-map.md`

- A BC's implementation status changed (e.g., new layer completed, atomic switch done).
- A new cross-BC dependency was introduced or an existing one was removed.
- A new BC was created that isn't on the map yet.
- Coupling hotspots changed (e.g., a shared dependency was decoupled).

## When to suggest updating `.github/context/project-state.md`

- A BC moved from "blocked" to "ready" or vice versa.
- A blocker was resolved (e.g., DB migration approved).
- New active work started on a BC that isn't listed.

## When to suggest updating `docs/roadmap/` files

- A roadmap step was completed but the file still shows it as pending.
- A new blocker emerged that affects the roadmap dependency chain.
- A new roadmap file is needed for a newly planned BC implementation.

## When to suggest updating existing ADRs

- An ADR's `## Implementation Status` table is out of date after completing implementation steps.
- An ADR should be marked as `Superseded by ADR-XXXX` after a newer decision replaced it.
- A `## Conformance checklist` needs a new item based on a lesson learned during implementation.
- The implementation still fits the same architectural decision, but the current ADR text or examples are stale.

## When to suggest docs/ADR updates because code drifted

- The implemented code differs meaningfully from the current ADR or docs wording.
- The behavior is still covered by an existing ADR, but that ADR now needs amendment or clarification.
- The behavior introduces a genuinely new architectural decision that no existing ADR covers.

**How to suggest**: _"Implementation and docs diverged here. If this is a new decision, consider a new ADR. If the decision already exists, update the current ADR/docs so the repo and guidance match again."_

## When to suggest updating the Copilot environment (`.github/`)

- A docs change alters meaning, architecture guidance, workflow, or navigation.
- A new human-facing router file is added under `docs/` (for example `docs/README.md`).
- ADR structure, naming, or routing changed and prompts/agents/instructions still point at old paths.
- A prompt, agent, or instruction now needs extra context because documentation evolved.

**How to suggest**: _"This docs change affects how Copilot should route or interpret the repo. Consider updating the `.github` environment (usually `docs-index.instructions.md`, prompts, agents, `.sln`, and changelog) or run `@copilot-setup-maintainer`."_

## When to suggest a new skill file

- The same manual fix has been performed 3+ times across recent sessions on the same area (e.g. "every PL/DE glossary edit needs the same 4-step verification") and no skill currently captures it.
- A non-trivial procedure (5+ steps, conditional branches, verification gates) was just executed ad-hoc and would benefit the next reader.
- A correction in `agent-decisions.md` was promoted (`Status: Promoted → <ref>`) but the reference points at prose instructions instead of a SKILL.md.

**How to suggest**: _"This procedure has come up repeatedly / is non-trivial. Consider promoting it to a skill at `.github/skills/<name>/SKILL.md` (see existing skills for the template shape)."_

## When to suggest a new eval query

- A parity audit (`tools/rag/compare_queries.py`) shows a corpus file consistently outside the top-5 across multiple queries — likely no targeted query exists for it.
- A new doc was just ingested and `.github/skills/rag-eval-coverage/SKILL.md` would flag it as uncovered.
- A user reports "RAG returns the wrong file for `<question>`" and grep shows no query in `queries.yaml` targets the expected file.

**How to suggest**: _"This file appears to lack eval-query coverage. Consider running `.github/skills/rag-eval-coverage/SKILL.md` and adding a query via `.github/skills/generate-eval-questions/SKILL.md`."_

## When to suggest a new memory entry (or promotion)

- The same correction appears for the 2nd time in `agent-decisions.md` — it's now a candidate for promotion (per `pre-edit.instructions.md` rule).
- A user explicitly says "remember this" or "next time, do X" and the constraint isn't already in `/memories/repo/` or `/memories/`.
- A workspace-specific convention was discovered (e.g. "in this repo, BCs are always named with `Bc<Name>` prefix") that isn't documented anywhere.

**How to suggest**: _"This correction has now appeared 2x in agent-decisions.md / is a workspace convention. Consider promoting to `/memories/repo/<topic>.md` (or `/memories/<topic>.md` if it applies across all workspaces)."_

## When to suggest a new ADR (recurring patterns)

- A pattern was shipped 2+ times across different BCs without an ADR recording the decision.
- A workaround or "we always do X here" emerged organically and would surprise the next contributor.
- Cross-BC behaviour was added (event flow, shared primitive, integration adapter) without an ADR justifying it.

**How to suggest**: _"This pattern has shipped in multiple places without an ADR. Consider creating one via `@adr-generator` or copying `.github/templates/adr.template.md` to capture the rationale before context is lost."_

## Rule: suggest, never auto-apply

- Do NOT create or modify documentation files without explicit human approval.
- Present the suggestion with: what file, what change, and why.
- If multiple documentation updates are needed, list them all at once so the human can batch-approve.
