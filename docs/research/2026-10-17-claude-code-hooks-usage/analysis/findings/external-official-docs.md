# Findings — official vendor documentation

Gather-phase evidence only. All pages fetched 2026-10-17. Reliability **High** (vendor doc, opened this run) unless noted. Quotes are verbatim excerpts.

## D1 — Anthropic, Claude Code "Hooks reference"

URL: https://code.claude.com/docs/en/hooks — no publication date on page; references features up to v2.1.299.

| # | Topic | Quote / fact |
|---|---|---|
| D1.1 | Event list | 30+ events incl. `SessionStart`, `Setup`, `UserPromptSubmit`, `UserPromptExpansion`, `PreToolUse`, `PermissionRequest`, `PermissionDenied`, `PostToolUse`, `PostToolUseFailure`, `PostToolBatch`, `Notification`, `MessageDisplay`, `SubagentStart/Stop`, `TaskCreated/Completed`, `Stop`, `StopFailure`, `TeammateIdle`, `InstructionsLoaded`, `ConfigChange`, `CwdChanged`, `DirectoryAdded`, `FileChanged`, `WorktreeCreate/Remove`, `PreCompact`, `PostCompact`, `PreModelSwitch`, `PostModelSwitch`, `Elicitation`, `ElicitationResult`, `SessionEnd`. |
| D1.2 | Handler types | `command`, `http`, `mcp_tool`, `prompt`, `agent` ("Agent hooks are experimental"). "All matching hooks run in parallel." |
| D1.3 | Exit codes | Exit 0 = success, JSON parsed. Exit 2 = blocking error on events that can block. "Exit code 1 without valid JSON output is a non-blocking error … If your hook is meant to enforce a policy, use `exit 2`." |
| D1.4 | Default fail mode | "On most events, when a hook fails or times out, Claude Code still carries out the action. A policy hook with a wrong path or a crashing script then lets everything through. To block the action instead, set `"onFailure": "block"` … Requires Claude Code v2.1.295 or later." |
| D1.5 | Timeouts | Defaults: 600 s for `command`/`http`/`mcp_tool`; lowered to 30 s on `UserPromptSubmit`. "A timed-out `command` … hook doesn't block the tool call … so don't count on a stalled hook to act as a gate." |
| D1.6 | Windows | Shell form runs in "Git Bash on Windows, or PowerShell when Git Bash isn't installed." "On Windows without Git Bash, a shell-form `PreToolUse` or `PermissionRequest` command hook that fails with `onFailure` unset can instead block the tool call." File paths "arrive with backslash separators"; "A hook that matches only `Bash` never fires" where PowerShell tool replaces Bash. |
| D1.7 | Exec vs shell form | "Set `args` whenever the hook references a path placeholder"; exec form needs a real executable; "The `node` plus script-path pattern works on every platform". |
| D1.8 | Hard enforcement caveat | On the `if` field: "Because the `if` filter is best-effort, use the permission system rather than a hook to enforce a hard allow or deny." |
| D1.9 | Static instructions | "For instructions that never change, prefer CLAUDE.md. It loads without running a script." Context text should be "factual statements rather than imperative system instructions" (prompt-injection defenses). |
| D1.10 | Deny reason routing | PreToolUse `permissionDecisionReason`: "For `"deny"`, shown to Claude." Precedence across hooks: `deny > defer > ask > allow`. |
| D1.11 | Loop guard | Stop hooks: `stop_hook_active`; "8-consecutive-continuation cap … Claude Code overrides the next block and ends the turn." |
| D1.12 | Escape hatch / inventory | `/hooks` read-only browser listing source of each hook. `"disableAllHooks": true`; "There is no way to disable an individual hook while keeping it in the configuration." |
| D1.13 | Debug | `claude --debug` / `--debug-file`; since v2.1.296 per-hook duration lines (`finished with status 0 (31ms)`), useful to find slow hooks. |
| D1.14 | Skill/agent-scoped hooks | Hooks may be declared in skill or subagent frontmatter; skill hooks stay registered "for the rest of the session". |
| D1.15 | Trust | Interactive sessions hold back settings-file hooks until workspace trust is accepted; `-p`/SDK sessions treat the folder as trusted. |
| D1.16 | Security | "Command hooks execute shell commands with your full user permissions." |

## D2 — Anthropic, "Automate actions with hooks" (hooks guide)

URL: https://code.claude.com/docs/en/hooks-guide — no date on page.

| # | Quote / fact |
|---|---|
| D2.1 | "Hooks … give you deterministic control: certain actions always happen rather than relying on the LLM to choose to run them." |
| D2.2 | Use cases shown: notifications (`Notification`), auto-format after edits (`PostToolUse Edit\|Write`), block edits to protected files (`PreToolUse`, exit 2), re-inject context after compaction (`SessionStart` matcher `compact`), audit config changes (`ConfigChange`), reload env (`CwdChanged`/`FileChanged`), auto-approve specific prompts (`PermissionRequest`). |
| D2.3 | Block-edit example: "Claude receives feedback explaining why the edit was blocked, so it can adjust its approach." |
| D2.4 | "For decisions that require judgment rather than deterministic rules, you can also use prompt-based hooks or agent-based hooks." |
| D2.5 | "Hooks and permission modes: `PreToolUse` hooks fire before any permission-mode check … A hook that returns `deny` blocks the tool even in `bypassPermissions` mode." Hooks "can tighten restrictions but not loosen them past what permission rules allow." |
| D2.6 | Coverage gap: "Claude can also create or modify files by running shell commands. If your hook must see every file change … add a `Stop` hook that scans the working tree … or `FileChanged`." |
| D2.7 | Troubleshooting list: hook not firing, `jq` missing, shell profile output before JSON ("Hook JSON has no effect"), fields at wrong level silently ignored, Stop hook block cap. |

## D3 — Anthropic blog, "Steering Claude Code: when to use CLAUDE.md, skills, hooks, and subagents"

URL: https://claude.com/blog/steering-claude-code-skills-hooks-rules-subagents-and-more — dated **2026-06-18**, author Michael Segner (Anthropic). Reliability **High** for vendor intent; single author.

| # | Quote / fact |
|---|---|
| D3.1 | "Use hooks for anything that should happen deterministically: running linters after edits, posting to Slack on completion, or blocking specific commands before they execute." |
| D3.2 | "'Every time X, always do Y' in CLAUDE.md. If the behavior should happen reliably … use a hook … The model choosing to run a formatter is different from the formatter running automatically." |
| D3.3 | "'Never do this' in CLAUDE.md … an instruction is the wrong tool … A real guardrail needs to be deterministic, and the enforcement methods are hooks and permissions." Managed settings "are the only way to enforce a deterministic, organization-wide guardrail." |
| D3.4 | "A 30-line procedure in CLAUDE.md. Procedures belong in skills." |
| D3.5 | Hooks: "low context cost because they are code that the harness runs rather than instructions to Claude"; "Bypass compaction entirely." Prompt/agent hook types "use Claude's judgment rather than a set of rules". |

## D4 — Microsoft, VS Code "Configure agent hooks in Visual Studio Code (Preview)"

URL: https://code.visualstudio.com/docs/copilot/customization/hooks (redirects to /docs/agent-customization/hooks) — "Edit this page 10/14/2026".

| # | Quote / fact |
|---|---|
| D4.1 | Purpose: "automate workflows, validate operations, create audit trails, or enforce policies independently of the language model." |
| D4.2 | Harness-dependent: Copilot harness uses "Shared Copilot SDK implementation" (same as Copilot CLI); Claude harness uses Claude Agent SDK; Codex; Cloud; Local. "Some harnesses discover the same hook files, such as `.github/hooks/*.json` or `.claude/settings.json`. This file compatibility does not make their behavior identical." |
| D4.3 | Local harness locations: `.github/hooks/*.json`; `.claude/settings.json` "Requires `chat.useClaudeHooks`, which is off by default"; Claude format: "Local parses nested commands but ignores matcher values, so every command for the event runs." |
| D4.4 | "The `chat.useHooks` setting controls Local hook execution and is on by default. Workspace hook files are subject to Workspace Trust." |
| D4.5 | Status: "The VS Code hooks experience is in Preview … hooks in the Copilot SDK are generally available." |
| D4.6 | Security: "Treat hook configuration and scripts as executable code. Require approval before an agent edits a script that a later hook can execute." |

## D5 — Microsoft, VS Code "Local hooks reference"

URL: https://code.visualstudio.com/docs/agents/reference/hooks-reference — "Edit this page 10/14/2026".

| # | Quote / fact |
|---|---|
| D5.1 | Exit codes: 0 success; 2 "Treat stderr as a blocking error and provide it to the model"; any other "Show a non-blocking warning to the user and continue processing." |
| D5.2 | Default `timeout` 30 s. Events: `SessionStart`, `UserPromptSubmit`, `PreToolUse`, `PostToolUse`, `PreCompact`, `SubagentStart`, `SubagentStop`, `Stop`. |
| D5.3 | Stop hook: "additional turns consume AI credits. Check `stop_hook_active` to prevent the agent from continuing indefinitely." |
| D5.4 | "The transcript file format is not a stable hook API." Tool names "differ between harnesses". |

## D6 — GitHub, "GitHub Copilot hooks reference" (Copilot CLI + cloud agent)

URL: https://docs.github.com/en/copilot/reference/hooks-reference — no date on page.

| # | Quote / fact |
|---|---|
| D6.1 | Surfaces: Copilot CLI and Copilot cloud agent. CLI sources: policy (`/etc/github-copilot/policy.d`, `C:\ProgramData\GitHub\Copilot\policy.d`, registry), `.github/hooks/*.json`, `~/.copilot/hooks/`, `.github/copilot/settings.json`, and "Cross-tool `.claude/settings.json` and `.claude/settings.local.json` files in the repository are also read." |
| D6.2 | Cloud agent: Linux sandbox, "Only the `bash` field … `powershell` entries are ignored", "Fully non-interactive … all tool permissions pre-granted"; `preToolUse` `"ask"` treated as `"deny"`. |
| D6.3 | Fail mode: "Command `preToolUse` hooks are **fail-closed** on errors—a crash or non-zero exit (including exit 2) denies the tool call … Command hook **timeouts are always fail-open**, even for `preToolUse` and admin-deployed policy hooks … HTTP `preToolUse` hooks are fail-open." Rationale: "a slow or unreachable hook must not silently block tool calls or work, even when the hook was deployed by an administrator as policy." |
| D6.4 | Default `timeoutSec` 30. Per-file malformed item dropped, sibling hooks still load. |
| D6.5 | `permissionDecisionReason` "Required when decision is `"deny"`." In autopilot `"ask"` is denied. |
| D6.6 | Runaway guard: "After 8 consecutive `block` continuations, the CLI overrides the hook." |
| D6.7 | Tool names: `bash`, `powershell`, `create`, `edit`, `view`, `glob`, `grep`, `task`, `web_fetch`, `ask_user`; Claude-format mapping `edit, str_replace_editor, apply_patch → Edit`, `create → Write`. |
| D6.8 | `disableAllHooks` per file (CLI + cloud) or in repo `settings.json` (CLI); policy hooks unaffected. |
| D6.9 | `userPromptSubmitted` output (`modifiedPrompt`) honoured only by SDK hooks; config-file hooks "have their output dropped". |
