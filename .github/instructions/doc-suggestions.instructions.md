---
applyTo: "**"
---

# During & After Work — Proactive Documentation Suggestions

Rule: suggest only, never auto-apply.
- Do not create/update docs without explicit user approval.
- Suggest file + change + reason.
- Batch multiple doc suggestions together.

## Suggest new ADR when

- Significant architectural decision changed.
- Existing ADR is intentionally bent/violated.
- New BC/aggregate/shared primitive lacks architecture record.
- Technology/library is added/replaced.
- Decision was debated and resolved in session/PR.

Suggestion template:
- "This change introduces <X>. Consider creating an ADR (use `@adr-generator` or `.github/templates/adr.template.md`)."

## Suggest updating bounded context map when

- BC implementation status changed.
- Cross-BC dependency added/removed.
- New BC exists but map is stale.
- Coupling hotspots changed.

Target file:
- `docs/architecture/bounded-context-map.md`

## Suggest updating project state when

- BC moved blocked<->ready.
- Blocker resolved.
- Active BC work started and not listed.

Target file:
- `.github/context/project-state.md`

## Suggest updating roadmap when

- Completed step still marked pending.
- New blocker impacts roadmap chain.
- New roadmap file is needed.

Target folder:
- `docs/roadmap/`

## Suggest updating existing ADRs when

- Implementation status table is stale.
- ADR should be superseded.
- Conformance checklist needs new item.
- ADR still valid but wording/examples are stale.

## Suggest docs/ADR drift fix when

- Code and docs diverged meaningfully.
- Existing decision covers behavior but docs need amendment.
- Behavior introduces genuinely new decision not covered.

Suggestion template:
- "Implementation and docs diverged here. If new decision: new ADR. If existing decision: amend current ADR/docs."

## Suggest Copilot config sync when

- Docs meaning/workflow/navigation changed.
- New top-level router docs were added.
- ADR routing/paths changed.
- Prompts/agents/instructions now need updated context.

Suggestion template:
- "Consider updating `.github` environment or running `@copilot-setup-maintainer`."

## Suggest new skill when

- Same manual fix appeared 3+ times.
- Non-trivial 5+ step procedure was done ad-hoc.
- Promoted correction points to prose, not a SKILL.

Target:
- `.github/skills/<name>/SKILL.md`

## Suggest new eval query when

- Parity audit shows file consistently outside top-k.
- New doc is ingested but uncovered by named eval queries.
- User reports wrong RAG file and no query targets expected file.

Suggestion template:
- "Run `.github/skills/rag-eval-coverage/SKILL.md` and add query via `generate-eval-questions`."

## Suggest memory promotion when

- Same correction appears 2nd time in agent-decisions.
- User says "remember this" and memory lacks the rule.
- Workspace convention discovered but undocumented.

Targets:
- `/memories/repo/<topic>.md` (repo scoped)
- `/memories/<topic>.md` (cross-workspace)

## Suggest recurring-pattern ADR when

- Same pattern shipped in 2+ BCs without ADR.
- Organic workaround became de-facto standard.
- Cross-BC behavior added without decision record.

Suggestion template:
- "Pattern shipped multiple times without ADR; consider recording with `@adr-generator`."
