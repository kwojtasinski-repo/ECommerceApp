# Findings — GitHub repositories, independent posts, issue trackers

Gather-phase evidence only. Fetched 2026-10-17. Only README / issue pages were opened; source code of external repos was **not** read.

## R — Repositories with real hook setups

| ID | Repo | What the hooks do | Fail mode / escape hatch / tests | Reliability |
|---|---|---|---|---|
| R1 | [diet103/claude-code-infrastructure-showcase](https://github.com/diet103/claude-code-infrastructure-showcase) (≈10k stars, last commit ≈3 months ago) | `UserPromptSubmit` "skill-activation-prompt" matches prompts against **`skill-rules.json`** (regex/keyword, optional AI classifier) and injects "MANDATORY SKILL ACTIVATION REQUIRED"; `PreToolUse` "skill-verification-guard" blocks edits until mandatory skills are loaded ("two-try blocking model"); `PostToolUse` trackers; optional `Stop` build/tsc checks. Also wires the same hooks for Codex via a thin adapter. | Kill switches: `SKIP_MANDATORY_SKILLS=true`, `PRETOOLUSE_SOFT_BLOCK`, remove `hooks` block. `fallback` AI mode "always degrades gracefully … never a broken prompt". `verify-setup.sh` 8-check health script. Hooks are bash: "won't run in plain cmd/PowerShell (Windows users: use WSL2)". | Medium (single maintainer README, self-described "production-tested") |
| R2 | [nizos/tdd-guard](https://github.com/nizos/tdd-guard) (≈2.4k stars, 80 releases) | Hook-driven TDD enforcement: "Blocks implementation without failing tests", "Prevents code beyond current test requirements"; validation by a configurable **LLM model**; "Session Control - Toggle on and off mid-session". Successor "Probity" targets Claude Code, Codex and **GitHub Copilot CLI**. | CI + security workflows; docs page "Strengthening enforcement - Prevent agents from bypassing validation". | Medium |
| R3 | [kenryu42/cc-safety-net](https://github.com/kenryu42/claude-code-safety-net) (≈1.6k stars, v2.6.13 released 2026-10-17) | Pre-execution guard: blocks destructive git/fs commands and secret access; parses commands so wrapping/flag reordering "does not hide it". Supports Claude Code, Codex, Cursor, Gemini CLI, **GitHub Copilot CLI** and others on Windows/macOS/Linux. Policy shared via committed `.cc-safety-net/`. | "**A broken config file never blocks anything.** It is not a sandbox." `doctor` command; CI, codecov, evals. | Medium |
| R4 | [hesreallyhim/awesome-claude-code](https://github.com/hesreallyhim/awesome-claude-code) | Used only as a pointer list. Lists further hook projects (not opened): Dippy (AST-based bash auto-approve), Gryph (YAML policy rules + SQLite audit), Claude Code Safety Guard, Plannotator (intercepts `ExitPlanMode` via hooks), agnix (linter for hooks config), multiple hook-driven status/observability tools. | — | Low (pointer only) |

## B — Independent engineering post

| ID | Source | Evidence | Reliability |
|---|---|---|---|
| B1 | Hidekazu Konishi, "Claude Code Hooks Complete Guide" — https://hidekazu-konishi.com/entry/claude_code_hooks_complete_guide.html — published 2026-06-07 | "A system prompt is a request. A hook is a guarantee." Anti-patterns: **"The slow hook tax"** ("A `PreToolUse` hook that takes two seconds adds two seconds to every matching tool call … Keep gate hooks fast … push anything non-decisive … to `async: true`"); confusing JSON shapes ("single most common bug"); exit-code mix-ups ("A guard hook that means to block but exits 1 will let the action through"); Stop-hook infinite loops; OS/env assumptions; **"Over-hooking. … If the cost of the model occasionally not doing something is low, CLAUDE.md guidance is lighter weight and easier to maintain. Reserve hooks for what must be deterministic."** Regex guard "is a guardrail, not a complete security boundary". Testing: pipe a sample event into the hook. | Medium for opinions; **Low for API details** — three statements contradict D1 (see 99-verification.md). Single author. |

## X — Issue tracker (anthropics/claude-code)

Titles below come from two search-result pages (`label:area:hooks windows`, `hooks fail open`); only X1 and X2 were opened in full.

| ID | Issue | Evidence | Reliability |
|---|---|---|---|
| X1 | [#100891](https://github.com/anthropics/claude-code/issues/100891) (opened ≈2026-10-10, open, has repro) | `onFailure: "block"` does **not** block on Windows when `powershell.exe -File <missing script>` exits `0xFFFD0000`; logged as `finished with status 0`. "Policy hooks on Windows that reference a wrong or deleted script path silently pass the action even with `onFailure: "block"` set." Workaround: launcher checks file existence and exits 1. Re-measured on 2.1.296. | High (detailed repro), single reporter |
| X2 | [#100054](https://github.com/anthropics/claude-code/issues/100054) (opened ≈2026-10-03) | User-written hook refused 26 tool calls in one day ("READ-FIRST BLOCK") across 14 agents; "Impact: Medium - Extra work". One of 19 refusal types logged by the same reporter (#100053–#100060), incl. "DESTRUCTIVE ACTION BLOCKED (false positive)". | Medium; **single author** for the whole cluster |
| X3 | #102649 (title only) | "Linux sandbox: … ~/.claude/hooks vanish and all hooks fail open". | Low (title only) |
| X4 | #100167 (title only) | "All project hooks silently disappear for the rest of a session after the project's volume dismounts and remounts (Windows, Google Drive)". | Low (title only) |
| X5 | #101311 (title only) | Plugin PostToolUse hook "fails on Windows usernames with a space (unquoted hook path)". | Low (title only) |
| X6 | #100634 (title only) | "/goal clear … the goal's Stop hook never lets the turn end". | Low (title only) |
| X7 | #102858 (title only) | "Hooks in command/skill frontmatter stop firing after the command's own turn (docs say 'rest of the session')". | Low (title only) |
| X8 | Search count | `is:issue label:area:hooks windows` → 535 results; `is:issue hooks fail open` → 1503 results (full-text, noisy). | Low as a metric |
