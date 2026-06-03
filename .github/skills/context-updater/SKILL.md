---
name: context-updater
description: >
  Updates .github/context/*.context.md and context files when codebase patterns change.
  Use after approving a new ADR, changing a core pattern (base class, DI convention, etc.),
  or when context files drift from the actual codebase.
argument-hint: "[context file name or 'all']"
---

# Context Updater

Keeps distilled context files in sync with approved ADRs and actual codebase patterns.

---

## When to Use

- After a new ADR is approved and merged
- After a significant pattern change (new base class, new BC convention, new shared primitive)
- When a context file is observed to be out of date (code and docs diverged)
- After `@spec-writer` or `@copilot-setup-maintainer` suggest running this skill

---

## Step 1 — Identify what to update

If a specific file was named (e.g. `anti-patterns-critical`), update only that file.
If `all` was specified or no argument given, check all files in `.github/context/`.

---

## Step 2 — Source of truth per context file

| Context File | Read these sources |
|---|---|
| `anti-patterns-critical.context.md` | `.github/instructions/safety.instructions.md` + `.github/instructions/dotnet.instructions.md` — BLOCKS MERGE sections |
| `anti-patterns-advisory.context.md` | `.github/instructions/dotnet.instructions.md` — NON-BLOCKING / advisory sections; P2 and P3 rules |
| `project-state.md` | Current BC migration status — check `ECommerceApp.Application/` and `ECommerceApp.Infrastructure/` for new BC folders; cross-reference ADRs |
| `known-issues.md` | Active known bugs and KI-NNN tracking refs — verify which issues are resolved by searching recent test additions |
| `repo-index.md` | Project folder map — update when new projects, major folders, or layer boundaries are added |
| `agent-decisions.md` | Correction log — append new entries after AI corrections; verify old entries are still valid |

---

## Step 3 — Update rules

- Use `query_docs("<context area>")` (RAG) to check the most recent ADR covering the area before reading files directly
- Read the source files listed above for the file(s) to update
- Update only what has **actually changed** — preserve correct existing content
- Keep each context file ≤ 120 lines; trim examples if needed
- `anti-patterns-critical.context.md` contains only BLOCKS MERGE rules — no P2/P3 advisory
- `anti-patterns-advisory.context.md` contains only P2/P3 — never BLOCKS MERGE
- After updating, report what changed and why

---

## Step 4 — Verify

Confirm the updated file:
- Reads cleanly as a quick-reference cheatsheet
- Contains no outdated patterns (e.g. references to removed BCs, old base classes)
- Examples match actual code in the codebase (spot-check one concrete example per rule)

---

## Step 5 — Report and suggest

After updating, suggest:
- Running `@copilot-setup-maintainer` if the change affects docs-index or copilot-instructions.md routing
- Updating the relevant ADR status table if the pattern became the new standard
