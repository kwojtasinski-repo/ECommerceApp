# Verification — cross-source checks

Date: 2026-10-17. Each row compares two sources that were both opened in this run.

## V1 — ADR-0033 / repo code vs current vendor docs

| Claim (repo) | Vendor doc | Result |
|---|---|---|
| Copilot CLI `preToolUse` is fail-closed on non-zero exit (I14) | D6.3: crash / non-zero / exit 2 → deny; **timeouts fail-open** | **Confirmed**, with a detail ADR-0033 does not mention: timeouts fail-open. |
| Copilot CLI loads `.claude/settings.json` (I14, I3) | D6.1: "Cross-tool `.claude/settings.json` … are also read" | **Confirmed.** |
| VS Code ignores matchers in Claude-format files (I14) | D4.3: "ignores matcher values" | **Confirmed.** Also needs `chat.useClaudeHooks` (off by default) — not in ADR. |
| "VS Code needs `chat.useHooks`" (I14) | D4.4: `chat.useHooks` "is on by default" | **Outdated wording**: the setting is still needed, but it's on by default. VS Code also now selects a *harness* (Copilot SDK / Claude / Local) that changes which payloads apply (D4.2). Not re-tested in this repo. |
| Copilot edit tools `edit`, `create` "UNVERIFIED" (I5) | D6.7 lists `create`, `edit` as CLI tool names | **Now documented.** The adapter comment can be updated. |
| Fail-open everywhere (I2, I3, I4, I14) | D1.4: Claude default is effectively fail-open; D6.3: Copilot defaults to fail-closed for `preToolUse`, which is why the adapters always exit 0 | **Consistent.** The repo normalizes two different vendor defaults into one fail-open behaviour. |
| Claude launcher in shell form, no `timeout` / `onFailure` (I1) | D1.6: on Windows **without Git Bash**, a failing shell-form `PreToolUse` hook with `onFailure` unset "can instead block the tool call"; D1.5: default timeout 600 s | **Risk not covered by ADR-0033.** On Windows the intended fail-open can turn into fail-closed if `node` fails to start. If the hook hangs, an edit can stall for up to 10 min before the call continues. Not reproduced. |
| PowerShell rewrites exit 2 → 1 (I14) | Not mentioned in D1/D5/D6 | **Not verified externally.** The repo avoids exit 2 entirely and uses JSON deny + exit 0, which all three docs support (D1.10, D5, D6.5). |
| Deny must explain next step (I8) | D1.10 reason "shown to Claude"; D2.3 "so it can adjust"; D6.5 reason "Required when decision is deny" | **Consistent.** |
| Shell not gated (I14) | D2.6: `Edit\|Write` hooks don't see Bash-made edits | **Consistent.** This is a documented limitation of tool-name matching. |
| `auto-cache.mjs` / `docs/rag/auto-cache-hook.md` exist (I15) | Workspace file search | **Contradicted**: both files are missing. The `agent-decisions.md` entry points to files that no longer exist. |

## V2 — Independent blog (B1) vs official reference (D1)

| B1 statement | D1 | Result |
|---|---|---|
| Hook resolution order "Managed > Local > Project > Plugin > User — a higher-priority layer wins" | "Hook entries merge across settings levels rather than replacing each other" | **Contradiction.** D1 is authoritative. |
| `defer` = "defer to the normal permission flow (equivalent to exiting 0 with no JSON)" | `defer` only honoured in `-p` mode; "In interactive sessions it logs a warning and ignores the hook result" | **Contradiction.** |
| `suppressOutput: true` hides stdout | "`suppressOutput` … Has no effect" | **Contradiction** (may be version drift; B1 dated 2026-06-07). |

Consequence: B1 is used only for practice and opinion claims (anti-patterns, "over-hooking"), not for API facts.

## V3 — Claude vs Copilot semantics (what a cross-tool hook must normalize)

| Aspect | Claude Code (D1) | Copilot CLI / cloud (D6) | VS Code Local (D5) |
|---|---|---|---|
| Config file | `.claude/settings.json` | `.github/hooks/*.json` (+ reads `.claude/settings.json`) | `.github/hooks/*.json` (+ Claude format if `chat.useClaudeHooks`) |
| Event names | PascalCase | camelCase or PascalCase (payload shape follows) | PascalCase |
| Default timeout | 600 s (30 s on `UserPromptSubmit`) | 30 s | 30 s |
| Exit 2 | block (blocking events) | deny for `preToolUse` / `permissionRequest`; warning elsewhere | block, stderr to model |
| Crash / non-zero on PreToolUse | non-blocking (action proceeds) unless `onFailure: "block"` | **deny** (fail-closed) | non-blocking warning |
| Timeout on PreToolUse | proceeds | proceeds (fail-open) | not stated |
| Loop cap on Stop | 8 continuations | 8 continuations | `stop_hook_active` advice |

Conclusion for verification: the main cross-tool difference is crash behaviour on `PreToolUse`. A shared core needs per-tool adapters, which is the architecture ADR-0033 chose.

## V4 — Hypotheses vs evidence (input to synthesis)

| H | Doc evidence | Practice evidence | Verdict |
|---|---|---|---|
| H1 good candidate = mechanical, costly error, always-on, fast | D2.1, D3.1-3, D1.5 | B1 slow-hook tax; R3 | **Supported**, with a nuance: for static tool allow/deny, D1.8/D3.3 recommend **permission rules**, which the user's list leaves out. |
| H2 heuristics with changing triggers → not a hook | D1.9 (static → CLAUDE.md); D2.4 prompt/agent hooks exist for judgment | R1 runs regex heuristics in hooks, but keeps the triggers in a data file (`skill-rules.json`); R2 uses an LLM inside a hook; X2 shows friction from frequent heuristic blocks (single author) | **Partially supported.** Practitioners do use heuristic hooks. The pain point is the trigger list being buried in hook code, not heuristics as such. |
| H3 hooks are Claude-only | D4, D5, D6 | R2 (Probity), R3 support Copilot CLI | **Contradicted.** Copilot CLI, Copilot cloud agent and VS Code all have hooks, but the semantics differ (V3). |
| H4 fail-open vs fail-closed must be deliberate; may be weak | D1.4 (`onFailure`), D6.3 (vendor split + rationale), I14 | X1, X3, X4: fail-closed configs that silently fail open | **Supported.** The choice is deliberate and documented in ADR-0033. Vendors themselves pick different defaults. |
| H5 deny message must be actionable | D1.10, D2.3, D6.5 | I8 | **Supported.** The loop claim was not verified: no opened source measures loops caused by unexplained denies. |
| H6 tests, README, escape hatch, inventory | D1.12, D1.13, D6.8 | R1 (verify script, kill switches), R2 (CI), R3 (doctor, CI) | **Supported** (practice from 3 independent repos). |
| H7 start-of-prompt match is stable but surprising | — | I6 (pasted-log incident) | **Rationale confirmed internally only.** No external source checked. |
| H8 order: instruction/skill → CI → hook | D3 frames it by requirement type, not by sequence; D3.3 "never do X" → hooks/permissions | B1 "over-hooking" | **Partially supported.** "Instruction first, hook only for invariants" holds. The CI-before-hook ordering was not verified, and permission rules are missing from the list. |
