# GO gate — permission guide for every agent

The GO gate blocks file-edit tools until the human starts a prompt with `IMPLEMENT` (`HOLD`/`STOP` re-lock).
Design and known gaps: [ADR-0033](../../docs/adr/0033/README.md). This file is the translation table: each
tool has its **own** permission system on top of the gate, and the two are independent.

> **For agents:** when an edit or command is refused, work out WHICH layer refused, then explain it to the
> user in your own words and in your tool's terms (below). Never run `gate-cli.mjs grant` on your own, never
> set `ECOMMERCEAPP_GATE=off`, and never edit through the shell to get around a refusal.

## Which layer said no?

| What you see | Layer | What to do |
|---|---|---|
| `Edits are locked until the user writes IMPLEMENT…` (shown as hook error / `Denied by preToolUse hook`) | GO gate | Ask the user; they answer with a prompt that **starts** with `IMPLEMENT`. Do not retry before they do |
| `Permission denied because no interactive user response was available`, `no one to approve …` | Copilot permission policy | Not the gate. In autopilot nobody can approve. Tell the user to switch to interactive mode or widen Copilot's allow rules |
| A normal "Allow this edit/command?" prompt | The tool's own permission prompt | Wait for the user's choice |
| A hook error that is not the GO-gate text | A broken hook | Report it; the gate is fail-open, so a crash must not lock you out |

`IMPLEMENT` only satisfies the gate. It never approves a tool's own permission prompt.

## Per tool

| Tool | Gate adapter | Own permission layer | Notes |
|---|---|---|---|
| Claude Code | `.claude/hooks/go-gate-adapter.mjs` (hooks in `.claude/settings.json`) | Permission modes (default asks; `acceptEdits` auto-allows edits) | Question tool: `AskUserQuestion`. Hooks loaded mid-session worked in testing. Headless/background behaviour not verified |
| Copilot CLI | `.github/hooks/go-gate-adapter.mjs` (`go-gate.json`) | Permission policy (`Manual Approval`, allow rules) — in autopilot unapproved calls are auto-denied | Question tool: `ask_user`, auto-answered in autopilot (never real consent). `copilot -p` loads no hooks. Subagents share the repo-wide marker |
| VS Code Copilot | `.github/hooks/go-gate-adapter.mjs` | Chat tool approvals | Needs `chat.useHooks`. Question tool: `#vscode/askQuestions`. Subagents reuse the parent session id |

## What the user can change (their decision, never the agent's)

- **Interactive mode** — safest; the tool asks for each write/command.
- **Narrow allow rules** — write tool plus `git`, `dotnet`, `node`; the gate still holds file edits. Recommended for autopilot. Check syntax for your CLI version (`/permissions`, `--allow-tool`).
- **Allow all tools** — the gate becomes the only guard, and an agent could `grant` itself or edit through the shell. Use only knowingly.

## Always allowed before GO

`.github/plans/**`, `docs/research/**`, `docs/reports/**` and the OS temp dir (not the gate's marker dir).
Shell is not gated: the gate is a guardrail, not a wall.
