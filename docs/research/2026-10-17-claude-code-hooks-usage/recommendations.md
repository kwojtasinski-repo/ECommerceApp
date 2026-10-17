# Recommendations for ECommerceApp

These are suggestions only. Nothing in the repo was changed. Every recommendation links to evidence IDs in [findings.md](findings.md) and `analysis/findings/`. The human decides, and any ADR or config change goes through the normal pipeline.

## A. What stays a hook

| # | Item | Recommendation | Why |
|---|---|---|---|
| A1 | **GO gate** (`.claude/settings.json`, `.github/hooks/go-gate.json`, `tools/hooks/`) | **Keep as a hook.** | It passes every checklist item in [decision-matrix.md §3](decision-matrix.md): the token is checked mechanically (I6), a miss costs a lot (I14), the check is fast and local, and permission rules cannot express "after the human said IMPLEMENT". ADR-0033 already rejected an instruction-only version. Its maintenance practices (tests, README, kill switch, config file) match what independent repos do (Q3: R1–R3, I9–I13). |
| A2 | Start-of-prompt token rule | **Keep.** Optionally add a hint (see C3). | The rule has a documented incident behind it: anywhere-in-text matching was unlocked by pasted logs (I6). No external source argues for looser matching. |

## B. What stays skill + instruction (not a hook)

| # | Item | Recommendation | Why |
|---|---|---|---|
| B1 | `linq-complexity-discovery` | **Keep as skill + instructions.** No blocking hook. | Its triggers are a heuristic list that will change. Vendor docs say static or judgment guidance belongs in CLAUDE.md, rules or skills (D1.9, D3.4). Blocking heuristic hooks cause friction (X2 ⚠1A). If automation is ever wanted, the pattern seen in practice is an **advisory** hook that only injects `additionalContext` and never denies, with the trigger list in a data file (R1 `skill-rules.json`), not in hook code. Claude-only. |
| B2 | Rules that must bind both tools | Keep the *explanation* in `.github/instructions/` + `CLAUDE.md`; enforce only through per-tool adapters, as the GO gate does. | Copilot has hooks, but their semantics differ (H3 contradicted, V3). |

## C. Changes worth considering for the GO gate (each needs human approval)

| # | Suggestion | Evidence | Risk if ignored |
|---|---|---|---|
| C1 | **Make Claude's fail-open explicit on Windows.** Either switch the launcher to exec form (`"command": "node", "args": ["${CLAUDE_PROJECT_DIR}/.claude/hooks/go-gate-adapter.mjs"]`; the adapter already self-runs on `argv[1]`), or add `"onFailure": "continue"` (needs Claude Code ≥ 2.1.295). | D1.6, D1.7, D1.4, V1 | On Windows **without Git Bash**, a failing shell-form `PreToolUse` hook "can instead block the tool call". The intended fail-open would then behave as fail-closed when `node` fails to start. Not reproduced here. |
| C2 | **Set an explicit `timeout`** (e.g. 10 s, the same as `timeoutSec: 10` in `go-gate.json`) on both Claude hook entries. | D1.5, I1, I4 | The default is 600 s on `PreToolUse`. A hung `node` could stall every edit for minutes before the call continues. |
| C3 | Optional UX: when a prompt contains `IMPLEMENT` but not at the start, have the `UserPromptSubmit` hook add a short context or `systemMessage` note ("GO token must start the prompt"). Keep matching unchanged. | I6, H7; D1 (UserPromptSubmit `additionalContext`) | The behaviour stays surprising (the "okay, let's try IMPLEMENT" case). Low risk either way. |
| C4 | Optional: surface degraded state. Errors currently go only to `<tmp>/ecommerceapp-go-gate/errors.log` (I10). A `SessionStart` check could print a `systemMessage` when the log has recent entries. | D1 (`systemMessage`), X3/X4 (silent fail-open, titles only) | A broken gate goes unnoticed, which is the price of fail-open. |
| C5 | Update the comment in `.github/hooks/go-gate-adapter.mjs` L13-15: `edit` and `create` are now documented Copilot CLI tool names. | D6.7, V1 | Cosmetic. |

## D. Is fail-open in `go-gate-adapter` a deliberate decision, and what do sources say?

- **Yes, it is deliberate and documented.** ADR-0033 Decision table: "Fail mode — Fail-open everywhere — A broken hook must never wedge agents". Under Alternatives: "Fail-closed — rejected: a hook bug would lock every agent out of the repo" (I14). The code matches: both adapters catch every error and exit 0, and the launcher exits 0 on import failure (I2–I4).
- **What sources say:**
  - Claude Code's default is fail-open; `onFailure: "block"` is an opt-in added in v2.1.295 (D1.4).
  - Copilot CLI is fail-closed on `preToolUse` crashes, but **fail-open on timeouts even for admin policy hooks**, because "a slow or unreachable hook must not silently block tool calls or work" (D6.3). This is the same reasoning ADR-0033 uses, applied by a vendor.
  - Fail-closed setups do leak in practice: `onFailure: "block"` was bypassed on Windows (X1, with repro), and hook directories have vanished and failed open (X3, X4, titles only).
- **Assessment (synthesis):** fail-open fits a *guardrail* whose purpose is to stop an agent from coding before approval. It is not a security boundary, and ADR-0033 says so ("guardrail, not a wall"). The remaining weaknesses are the Windows shell-form path (C1), the missing timeout (C2) and silent degradation (C4), not the fail-open choice itself.

## E. Documentation drift found (suggest only; per doc-suggestion rules)

| # | File | Drift | Suggested action |
|---|---|---|---|
| E1 | `docs/adr/0033/0033-go-gate-hooks.md` | Text: "VS Code needs `chat.useHooks`". The setting is now **on by default**. Claude-format files additionally need `chat.useClaudeHooks` (off by default). VS Code now picks a *harness* (Copilot SDK / Claude / Local) that changes payloads (D4.2–D4.4). Copilot timeouts being fail-open is not mentioned (D6.3). | Amend ADR-0033 "Findings" with a dated note. Consider re-testing VS Code, which ADR-0033 says was not re-tested. |
| E2 | `.github/context/agent-decisions.md` L54-70 | Points to `.github/hooks/auto-cache.mjs` and `docs/rag/auto-cache-hook.md`. Neither file exists (I15). | Mark the entry as superseded/removed, or restore the files. |
| E3 | Mechanism order (user's default "instruction/skill → CI → hook") | Leaves out **permission rules**, which vendor docs recommend for hard allow/deny (D1.8, D3.3). | Suggested order: instruction/skill → permission rules → hook (Claude + Copilot adapters) → CI as the backstop that also binds humans. The CI position is not source-verified. |

## F. Open questions for the human

1. Which **Claude Code version** is in use? `onFailure` (C1) needs ≥ 2.1.295.
2. Is **Git Bash** installed on every Windows machine that runs Claude Code here? This decides how urgent C1 is.
3. Should fail-open stay **silent**, or should a degraded gate be visible in the session (C4)?
4. Which **VS Code harness** do you use in practice (Copilot / Claude / Local)? Should the gate be re-tested there (E1)?
5. Is there an **Azure DevOps CI** step where `node --test tools/hooks/gate-core.test.mjs .claude/hooks/go-gate-adapter.test.mjs .github/hooks/go-gate-adapter.test.mjs` should run, so hook regressions fail the build?
6. Do you want ADR-0033 amended (E1) and the `agent-decisions.md` entry fixed (E2)? Both are docs changes and would trigger `@copilot-setup-maintainer`.
7. Should the pre-commit/CI criteria (Q4 gap) be researched separately? That needs approval to open pre-commit docs.
