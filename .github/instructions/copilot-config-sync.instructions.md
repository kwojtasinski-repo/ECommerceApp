---
description: "Auto-sync trigger for Copilot configuration changes. Fires when editing any .github/ or docs/ file."
applyTo: ".github/**, docs/**"
---

# Copilot Config Auto-Sync

> This file auto-loads when editing ANY file under `.github/` or `docs/`.
> It ensures configuration changes cascade correctly.

## After adding or editing a `docs/` file, check these cascading impacts:

### New ADR added (`docs/adr/*.md`)

If you created a new ADR file:

1. **`docs-index.instructions.md`** — add a new row to the ADR table in the correct numerical position with a concise "When to read" description.
2. **`copilot-instructions.md`** — increment the ADR count in § 2.
3. **`ECommerceApp.sln`** — add the new file to the `adr` solution folder.
4. **`COPILOT-SETUP-CHANGELOG.md`** — add an entry noting the new ADR.
5. Suggest: _"New ADR added. Run `@copilot-setup-maintainer` to sync docs-index, copilot-instructions, .sln, and changelog."_

### ADR renamed or removed (`docs/adr/*.md`)

If you renamed or deleted an ADR file:

1. **`docs-index.instructions.md`** — update or remove the corresponding row.
2. **`ECommerceApp.sln`** — update the `adr` solution folder entry.
3. Check if any prompt or instruction file references the old ADR and report (do NOT edit — report only).
4. Suggest: _"ADR changed. Run `@copilot-setup-maintainer` to cascade updates."_

### New roadmap file added (`docs/roadmap/*.md`)

If you created a new roadmap file:

1. **`docs-index.instructions.md`** — add a row to the roadmap table.
2. **`ECommerceApp.sln`** — add the file to the `roadmap` solution folder.
3. Suggest: _"New roadmap file added. Run `@copilot-setup-maintainer` to sync docs-index and .sln."_

## After editing a `.github/` file, check these cascading impacts:

### Anti-patterns changed (`context/anti-patterns-critical.context.md`)

If you added, removed, or renamed an anti-pattern rule:

1. **Code-reviewer agent** (`agents/code-reviewer.md`) — verify the anti-pattern scan section still matches.
2. **`copilot-instructions.md`** — if it has an anti-pattern quick-ref, verify it's current.
3. Suggest: _"Anti-patterns changed. Run `@copilot-setup-maintainer` to cascade updates to code-reviewer and changelog."_

### Instruction file added/removed/renamed (`instructions/*.instructions.md`)

If you added a new instruction file or changed its `applyTo:` glob:

1. **Code-reviewer agent** — update the conditional context loading table.
2. **`docs-index.instructions.md`** — add/update the row in the instruction files table.
3. **`copilot-instructions.md`** — add/update § 2 if applicable.
4. Suggest: _"Instruction file changed. Run `@copilot-setup-maintainer` to sync docs-index, code-reviewer, and changelog."_

### Agent/skill/prompt added/removed (`agents/`, `skills/`, `prompts/`)

1. **`docs-index.instructions.md`** — add/update the relevant table.
2. **`copilot-instructions.md`** — update § 2 navigation map.
3. **`COPILOT-SETUP-CHANGELOG.md`** — add entry.

### Context file changed (`context/*.md`)

1. **Code-reviewer agent** — if the changed context file is referenced in the reviewer's "load context" section, verify it's still correct.
2. **`docs-index.instructions.md`** — verify the context file table is current.

## Rule: suggest, do not auto-cascade

- **Always suggest** running `@copilot-setup-maintainer` for multi-file cascades.
- **Never silently edit** other config files — the user must approve.
- Single-file fixes (e.g., fixing a typo in one instruction file) don't need cascading.
