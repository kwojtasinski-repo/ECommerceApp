# Playbooks — long-form bootstrap & operations walkthroughs

This directory holds end-to-end **playbooks** — multi-stage runbooks an engineer (or
agent) can follow top-to-bottom to bring up a project component from scratch. Each
playbook invokes multiple `.github/skills/` in sequence with verification checkpoints
between stages.

## When to read a playbook (vs a skill)

- **Read a skill** when you need to do **one thing** (define an AdGuard allowlist,
  install a runtime, register an MCP client). Skills are focused, 200–300 lines,
  worked-example-driven.
- **Read a playbook** when you need to do **everything for a stack** in one sitting —
  brand-new project, fresh dev box, post-disaster restore. Playbooks reference the
  skills they use; they don't repeat skill content.

## Available playbooks

| Playbook | What it covers | Skills invoked | Time |
|---|---|---|---|
| [context-mode-bootstrap.md](context-mode-bootstrap.md) | Stand up the context-mode sandbox: AdGuard, Qdrant, SQLite, runtime image, MCP client, optional auto-cache hook | D1, D2, D3, E2, E3, E4, E5 | 60–90 min |
| [rag-bootstrap.md](rag-bootstrap.md) | Stand up RAG: Qdrant, Python + .NET HTTP servers, ingest, MCP client, optional auto-cache hook | E1, E4, optional E5 | 45–75 min |

## Order of operations for a brand-new project

If you're standing up BOTH RAG and context-mode for the first time:

```text
1. rag-bootstrap.md             (Stages 0–5: RAG up, ingested, smoke-tested)
2. context-mode-bootstrap.md    (Stages 0–6: sandbox up, hook wired)
   ↑ rag-bootstrap Stage 7 (auto-cache) WILL be skipped — context-mode isn't up yet
3. rag-bootstrap.md Stage 7     (auto-cache hook now installable; both halves present)
```

If you only need ONE of the two, run that playbook end-to-end and skip the
cross-references to the other.

## Conventions across all playbooks

- **Stages are numbered**, each with a "Checkpoint" line you must satisfy before
  proceeding.
- **Cross-platform**: code blocks use POSIX `sh` by default; PowerShell equivalents
  appear inline where syntax differs.
- **All file paths are workspace-relative** and use markdown links (per
  file-linkification rules) — no inline backticks for file names.
- **Verification commands at every step** — never skip them. A green Stage N means
  Stage N+1 can start; a red checkpoint means stop and debug before continuing.
- **Reference section at the bottom** — every playbook links the ADRs and skills it
  draws from.

## Adding a new playbook

If you find yourself walking through 4+ skills in sequence for a recurring scenario,
that's playbook material. Create a new file here following the shape of the existing
two:

1. Audience + estimated time
2. Pre-flight checklist
3. Stages 0–N with checkpoints
4. Troubleshooting flowchart
5. "What to do next" pointers
6. Reference section

Then append a row to the table above and update
[.github/instructions/docs-index.instructions.md](../../.github/instructions/docs-index.instructions.md).

## Reference

- [.github/skills/](../../.github/skills/) — focused single-task skills
- [.github/instructions/docs-index.instructions.md](../../.github/instructions/docs-index.instructions.md) — full routing table
- [docs/README.md](../README.md) — human-facing docs entry point
- [docs/adr/](../adr/) — architectural decisions playbooks cite
