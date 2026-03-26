---
description: "Auto-sync trigger for Copilot configuration changes. Fires when editing any .github/ file."
applyTo: ".github/**"
---

# Copilot Config Auto-Sync

> This file auto-loads when editing ANY file under `.github/`.
> It ensures configuration changes cascade correctly.

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
