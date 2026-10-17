# Source manifest

> **Update 2026-10-17 (after gathering):** sections A–E were opened as listed in `analysis/findings/` (IDs I*, D*, R*, B*, X*). Section F (pre-commit docs, other agents) was **not** opened. See `analysis/findings/98-rejected.md`.

Status legend: **read** = opened in this run; **planned** = to open after approval; **candidate** = needs Checkpoint 2.

## A. Internal (repo)

| Path | Purpose | Status |
|---|---|---|
| `.claude/settings.json` | Claude hooks: `UserPromptSubmit`, `PreToolUse` (matcher `Edit\|Write\|NotebookEdit\|MultiEdit`); launcher `.then(m=>m.run(),()=>process.exit(0))` | read |
| `.github/hooks/go-gate.json` | Copilot hooks: `sessionStart`, `userPromptSubmitted`, `preToolUse`; bash + powershell launchers, `timeoutSec: 10`, walk-up path resolution | read |
| `docs/adr/0033/0033-go-gate-hooks.md` | Design, fail-open decision, verified findings (PowerShell exit 2→1, Copilot CLI fail-closed, path drift, self-grant) | read |
| `.github/context/agent-decisions.md` (L54–70, L273–288, L331–334) | Prior hook corrections: auto-cache PostToolUse hook, VS Code native hooks (Preview), opaque hook chains | read (grep) |
| `.claude/hooks/go-gate-adapter.mjs` + `.test.mjs` | Claude adapter | planned |
| `.github/hooks/go-gate-adapter.mjs` + `.test.mjs` | Copilot adapter | planned |
| `tools/hooks/gate-core.mjs`, `gate-cli.mjs`, `gate.config.json`, `gate-core.test.mjs`, `README.md` | Core, escape hatch, config, tests, docs | planned |
| `docs/adr/0033/README.md` | ADR router | planned |
| `docs/rag/auto-cache-hook.md` | Existing PostToolUse hook (Copilot) | planned |
| `.claude/skills/linq-complexity-discovery/SKILL.md`, `.github/instructions/efcore.instructions.md` | Heuristic rule enforced via skill/instruction | planned |

## B. Official documentation (planned — URLs to be confirmed by opening; none cited yet)

| Vendor | Topic |
|---|---|
| Anthropic | Claude Code hooks reference (events, matchers, exit codes, JSON output, timeouts) |
| Anthropic | Claude Code hooks guide / getting started |
| Anthropic | Claude Code settings + permissions (interaction with hooks) |
| Microsoft | VS Code Copilot agent hooks (Preview) |
| Microsoft / GitHub | Copilot custom instructions (`.github/copilot-instructions.md`, `*.instructions.md`) |
| GitHub | Copilot CLI hooks / configuration |
| GitHub | Copilot coding agent (cloud) — hooks / setup steps |

## C. GitHub repositories (planned)

Search: `.claude/settings.json` containing `"hooks"` and `PreToolUse` / `PostToolUse` / `UserPromptSubmit` / `Stop`;
curated lists (e.g. "awesome claude code" collections) used only as pointers — each listed repo must be opened.
Target ≥ 5 independent authors.

## D. Engineering posts (planned)

Anthropic engineering blog posts on Claude Code practices; independent posts with concrete hook configs.

## E. Issue trackers (planned)

`anthropics/claude-code` issues tagged/labelled around hooks (Windows, exit code, latency, infinite loop,
permission conflicts); VS Code / Copilot CLI issues on agent hooks.

## F. Candidates requiring Checkpoint 2

- pre-commit framework docs (`pre-commit.com`) — only if Q4/Q5 need the mechanism's own semantics.
- Other agents' hook systems (Cursor, Codex, Gemini CLI) — only if a source compares them directly with Claude Code.
