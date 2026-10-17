# Findings — codebase (internal, read-only)

Gather-phase evidence only. No synthesis. Access date for all: 2026-10-17. Reliability: **High** (code/config opened, lines cited) unless noted.

| ID | Source | Evidence |
|---|---|---|
| I1 | `.claude/settings.json` L3-24 | Two Claude hook groups: `UserPromptSubmit` (no matcher) and `PreToolUse` with matcher `Edit\|Write\|NotebookEdit\|MultiEdit`. Both use **shell form** (`command` only, no `args`) running `node -e "...import(pathToFileURL(CLAUDE_PROJECT_DIR + '/.claude/hooks/go-gate-adapter.mjs')).then(m=>m.run(),()=>process.exit(0))"`. **No `timeout`, no `onFailure`** field set. |
| I2 | `.claude/hooks/go-gate-adapter.mjs` L3 | Comment: "Any failure here exits 0 (fail-open): a broken gate must never wedge the agents." |
| I3 | `.claude/hooks/go-gate-adapter.mjs` L42-45, L57-62 | Stands down when `COPILOT_CLI` is set (Copilot CLI also loads `.claude/settings.json`). `run()` catches any error, logs it, `process.exit(0)`. Deny is emitted as `hookSpecificOutput.permissionDecision: "deny"` JSON (exit 0). |
| I4 | `.github/hooks/go-gate.json` | Copilot hooks: `sessionStart`, `userPromptSubmitted`, `preToolUse`; separate `bash` and `powershell` launchers; `timeoutSec: 10`; launcher walks up from cwd to find the adapter; import failure → `process.exit(0)`. |
| I5 | `.github/hooks/go-gate-adapter.mjs` L1-20 | Comment: one script serves VS Code Chat (`hook_event_name, session_id, tool_name, tool_input`) and Copilot CLI (`sessionId, toolName, toolArgs`), "observed 2026-10-13, Phase 0 probe". "Any failure exits 0 (fail-open)". Deny printed in both JSON shapes. Edit-tool names `edit`, `create` are marked **"UNVERIFIED names kept for safety"** (L13-15). |
| I6 | `tools/hooks/gate-core.mjs` L65 | Comment on start-of-prompt token rule: "Anywhere-in-text matching was unlocked by pasted logs that quote the deny message." (rationale for H7 behaviour). |
| I7 | `tools/hooks/gate-core.mjs` L94 | Marker directory excluded from temp-dir allowance: "an agent must not be able to write its own GO marker." |
| I8 | `tools/hooks/gate-core.mjs` L100-108 | Deny reason is actionable: tells the agent to ask the user, not to retry, names `node tools/hooks/gate-cli.mjs grant <sessionId>`, distinguishes the gate from the tool's own permission layer, points to `tools/hooks/README.md`. |
| I9 | `tools/hooks/gate-core.mjs` L122-123 | Escape hatches in code: `ECOMMERCEAPP_GATE=off` disables; `CI` / `GITHUB_ACTIONS` bypass. |
| I10 | `tools/hooks/gate-core.mjs` L162 | Errors logged to `<os tmp>/ecommerceapp-go-gate/errors.log` (outside the repo, not surfaced in the session). |
| I11 | `tools/hooks/gate.config.json` | Rule data externalised: `goTokens`, `revokeTokens`, `allowedPathGlobs`, `markerTtlHours: 12`, `bypassEnv`, `disableEnv`, `freshGrantGraceSeconds`. |
| I12 | `tools/hooks/README.md` | Human-facing "which layer said no?" table; per-tool permission layer table; explicit agent prohibitions (no self-grant, no `ECOMMERCEAPP_GATE=off`, no shell bypass). |
| I13 | Tests: `tools/hooks/gate-core.test.mjs` (25 `it`), `.claude/hooks/go-gate-adapter.test.mjs` (8), `.github/hooks/go-gate-adapter.test.mjs` (15) | 48 unit tests incl. fail-open on malformed stdin (both adapters), pasted-log token, case sensitivity, TTL, traversal, marker self-write, CI bypass. |
| I14 | `docs/adr/0033/0033-go-gate-hooks.md` | Decision table: "Fail mode — Fail-open everywhere — A broken hook must never wedge agents"; Alternatives: "Fail-closed — rejected: a hook bug would lock every agent out of the repo". Verified-live findings: PowerShell rewrites exit 2→1; relative paths drift; Copilot CLI preToolUse fail-closed on non-zero; Copilot CLI loads `.claude/settings.json`; `copilot -p` loads no repo hooks; VS Code needs `chat.useHooks`, ignores matchers in Claude-format files; agents can self-grant. VS Code not re-tested in this repo. |
| I15 | `.github/context/agent-decisions.md` L54-61 | Records a host-side Copilot `PostToolUse` hook `.github/hooks/auto-cache.mjs` and doc `docs/rag/auto-cache-hook.md`. **Neither file exists in the workspace today** (file search 2026-10-17 found only `go-gate*` under `.github/hooks/` and no `auto-cache*` anywhere). |
| I16 | `.github/context/agent-decisions.md` L273-278 | 2026-05-27 correction: "VS Code Copilot Chat DOES have native hook support (Preview) — default location `.github/hooks/*.json`". Lesson: probe CLI commands referenced by hooks before relying on them (exit 127 would fail silently). |
| I17 | `.github/instructions/agent-workflow.instructions.md`, `CLAUDE.md` | `linq-complexity-discovery` is enforced by skill + instruction text in both tools ("use the skill when writing LINQ over a DbSet"), no hook. |

## Not opened (internal)

- `tools/hooks/gate-cli.mjs` body (only referenced by deny message and README).
- `docs/adr/0033/README.md` router.
