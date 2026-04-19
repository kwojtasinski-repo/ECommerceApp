---
applyTo: "**"
---

# Pre-Edit Checklist (mandatory before any edit)

Before proposing or committing changes, perform these steps:

1. **Read context** — Read the entire target file(s) and relevant related files (Controllers, services, repository code, tests).
2. **Read ADRs** — Read ADRs in `docs/adr/` that are directly relevant to the area being changed — not all of them.
3. **Read instructions** — Read the relevant per-stack instructions under `.github/instructions/`.
4. **Search for impact** — Search for usages and migration impact (references, database migrations, API clients) and list affected areas.
5. **Validate locally** — Run `dotnet restore`, `dotnet build`, and `dotnet test` (or explain why not possible).
6. **Include tests** — Include tests for any behavioral change.
7. **Rollback plan** — Include a short rollback/mitigation plan for risky changes.
8. **PR for review** — Open a pull request for review; do not merge without human approval unless explicitly asked.

Document completion of these steps in the PR description.

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

## Rule: suggest, never auto-apply

- Do NOT create or modify documentation files without explicit human approval.
- Present the suggestion with: what file, what change, and why.
- If multiple documentation updates are needed, list them all at once so the human can batch-approve.
