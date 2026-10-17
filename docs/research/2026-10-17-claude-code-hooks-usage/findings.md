# Findings — How coding agents use hooks, and when a hook is the right mechanism

- **Date:** 2026-10-17 (all sources accessed this day)
- **Method:** `research-gatherer` skill, interactive. Plan approved by the human, then evidence gathered, cross-checked and summarised.
- **Raw evidence:** [codebase](analysis/findings/codebase-hooks.md) (I*), [official docs](analysis/findings/external-official-docs.md) (D*), [repos/blogs/issues](analysis/findings/external-repos-blogs-issues.md) (R*, B*, X*), [verification](analysis/findings/99-verification.md) (V*), [rejected](analysis/findings/98-rejected.md).

**Labels:** **[DOC]** = vendor documentation fact · **[PRACTICE]** = observed in real repos or issues · **[OPINION]** = an author's recommendation · **[REPO]** = this repository. Reliability: H (high) / M (medium) / L (low). ⚠1A = all evidence comes from one author.

---

## Q1. Categories of use and which events serve them

| Category | Typical event(s) | Evidence | Label / rel. |
|---|---|---|---|
| Security gate / command blocking | `PreToolUse` (deny via JSON or exit 2), `PermissionRequest` | D1.10, D2.2, D3.1; R3 (destructive git/fs + secrets); B1 §7.2 | DOC H, PRACTICE M |
| Workflow gate (approval before edits) | `UserPromptSubmit` + `PreToolUse` | I1-I14 (GO gate); R1 (edits blocked until skills loaded); R2 (edits blocked without failing test) | REPO H, PRACTICE M |
| Format / lint after edit | `PostToolUse` matcher `Edit\|Write`; `FileChanged` for edits made by any process | D2.2, D2.6, D3.1; B1 §7.1 | DOC H |
| Context injection | `SessionStart` (incl. matcher `compact`), `UserPromptSubmit`, `SubagentStart`, `PostToolUse` `additionalContext` | D1.9, D2.2; D6 (`sessionStart`, `subagentStart`); R1 (skill activation prompt) | DOC H, PRACTICE M |
| Audit / logging / observability | `PostToolUse` (`async: true`), `ConfigChange`, `InstructionsLoaded`, HTTP hooks | D2.2; B1 §7.3; R4 lists several dashboards | DOC H, PRACTICE L-M |
| Notifications | `Notification`, `Stop` | D2.2; R4 (notifier projects) | DOC H |
| Completion / quality gates | `Stop`, `SubagentStop`, `TaskCompleted` (with loop guard) | D1.11, D5.3, D6.6; R1 optional `Stop` build checks | DOC H |
| Prompt validation | `UserPromptSubmit` `decision: block` | D1 (UserPromptSubmit); B1 §7.7 | DOC H |
| Judgment-based checks | `type: "prompt"` / `"agent"` hooks | D1.2, D2.4; R2 (LLM-validated TDD) | DOC H; agent hooks are **experimental** |

**Exit-code / output semantics (as documented on 2026-10-17)** [DOC H]:
- **Claude Code** (D1.3-D1.5): exit 0 → JSON parsed. Exit 2 → block on blocking events. Any other exit, a crash or a timeout → **the action proceeds**, unless `onFailure: "block"` is set (v2.1.295+).
- **Copilot CLI/cloud** (D6.3): `preToolUse` crash or non-zero exit → **deny**. Timeouts → **proceed**. HTTP hooks → proceed.
- **VS Code Local** (D5.1): exit 2 → block. Other exits → warning.
- **In all three:** the deny reason is shown to the model (D1.10, D5.1, D6.5). Copilot requires it when denying.

## Q2. Documented problems

| Problem | Evidence | Label / rel. |
|---|---|---|
| **Latency.** Synchronous hooks sit on the critical path. Default timeouts are long (Claude 600 s; 30 s on `UserPromptSubmit`). | D1.5; B1 "slow hook tax"; D1.13 adds per-hook duration logging (v2.1.296) | DOC H, OPINION M |
| **Silent fail-open.** A hook that is missing, crashes or times out lets the action through by default (Claude). Even explicit fail-closed can leak on Windows. | D1.4, D1.5; X1 (`onFailure:"block"` bypassed by PowerShell `0xFFFD0000`, repro); X3, X4 (hooks vanish → all fail open; titles only) | DOC H, PRACTICE H (X1) / L (X3-X4) |
| **Fail-open vs fail-closed differs by vendor.** | V3: Claude proceeds on crash, Copilot denies on crash; both proceed on timeout | DOC H |
| **Windows specifics.** Shell selection (Git Bash vs PowerShell). Without Git Bash a failing shell-form hook can *block*. Backslash paths. `Bash` matcher misses the PowerShell tool. Unquoted paths with spaces. | D1.6, D1.7; X1; X5 (title); I14 (PowerShell exit 2→1, repo-only) | DOC H, PRACTICE M |
| **Silent misconfiguration.** Wrong JSON shape is ignored. Shell-profile output corrupts the JSON. A wrong path means the hook never runs. | D2.7; B1 "confusing the two JSON shapes" | DOC H |
| **Loops.** A Stop hook that always blocks. Both vendors cap it at 8 continuations. | D1.11, D6.6, D5.3; X6 (title) | DOC H |
| **False positives / friction from heuristic gates.** One user logged 26 refusals in a day from their own hook. | X2 cluster ⚠1A | PRACTICE M |
| **Coverage gaps.** Tool-name matchers miss shell-made edits. `@` file mentions skip `PreToolUse`. `copilot -p` loads no repo hooks. | D2.6, D1 (`@` mention warning), I14 | DOC H, REPO H |
| **Conflict with permission layers.** Hooks can tighten but not loosen permissions. In Copilot, the permission policy is a separate layer ("no one to approve"). | D2.5, I14, I12 | DOC H, REPO H |
| **Cross-harness drift.** Same file, different behaviour per harness. Transcript format is not a stable API. | D4.2, D5.4 | DOC H |
| **Security.** Hooks run with full user privileges. `-p` sessions trust repo hooks without asking. | D1.15, D1.16, D4.6 | DOC H |

## Q3. Maintenance patterns teams use

| Pattern | Evidence | Label |
|---|---|---|
| Unit tests that pipe sample JSON into the hook | B1 §6.3; R2, R3 run CI; I13 (48 tests here) | OPINION + PRACTICE M, REPO H |
| Health / doctor command | R1 `verify-setup.sh` (8 checks); R3 `doctor` | PRACTICE M |
| Kill switches and escape hatches | R1 `SKIP_MANDATORY_SKILLS`, soft-block flag; D1.12 / D6.8 `disableAllHooks`; I9 `ECOMMERCEAPP_GATE=off` | DOC H, PRACTICE M, REPO H |
| Rule data kept outside hook code | R1 `skill-rules.json`; R3 committed `.cc-safety-net/` policy; I11 `gate.config.json` | PRACTICE M, REPO H |
| Discoverable inventory | D1.12 `/hooks` menu; D4 Agent Customizations editor / `/hooks`; I12 README | DOC H, REPO H |
| Thin per-tool adapters over a shared core | R1 Codex shim; R3 supports ~16 CLIs; I3/I5 | PRACTICE M, REPO H |
| Debug logs | D1.13; D4 "GitHub Copilot Chat Hooks" output channel; I10 temp `errors.log` | DOC H |
| Explicit "this is a guardrail, not a sandbox" disclosure | R3; B1 §7.2; I14 | PRACTICE M |
| Versioning rules | Not found as an explicit practice beyond git-committed config (R3) | **Not confirmed** |

## Q4. When people choose skill / instruction / CI over a hook

| Criterion given | Source | Label |
|---|---|---|
| Static instructions / conventions → CLAUDE.md or rules, not hooks | D1.9; D3 table | DOC H |
| Procedures → skills | D3.4 | DOC H (vendor, single author) |
| "Every time X, always do Y" → hook | D3.2 | DOC H |
| "Never do this" → hook **or permissions**. For an org-wide rule, use managed settings. | D3.3 | DOC H |
| Hard allow/deny on tool + argument patterns → **permission rules**, because hook `if` filtering is best-effort | D1.8 | DOC H |
| Low cost of an occasional miss → instruction ("over-hooking") | B1 §10 | OPINION M ⚠1A |
| Judgment needed → prompt/agent hooks *or* skills | D2.4, D3.5 | DOC H |
| CI / pre-commit instead of a hook | **No opened source states criteria.** Pre-commit docs were not fetched (98-rejected). | **Not verified** |

## Q5. Copilot equivalents and combining both tools

- **Hooks exist in Copilot.** They are available in Copilot CLI, Copilot cloud agent (`.github/hooks/*.json`) and VS Code (Preview; Copilot SDK hooks GA) (D4.5, D6.1) [DOC H]. The user's assumption that hooks are Claude-only is **outdated** (V4/H3). This repo already uses them (I4).
- **File compatibility is partial.** Copilot CLI reads `.claude/settings.json` (D6.1). VS Code reads it only with `chat.useClaudeHooks`, and then ignores matchers (D4.3). "This file compatibility does not make their behavior identical" (D4.2) [DOC H].
- **Semantics differ.** The table in V3 lists timeouts, crash handling, event-name casing, payload casing and tool names. Cloud agent has no user, so `ask` becomes `deny` (D6.2) [DOC H].
- **Combination pattern seen in practice.** A tool-agnostic core with thin adapters (I3/I5, R1, R3). Rules shared via committed config (R3, I11) [PRACTICE M, REPO H].
- **Instructions as the shared layer.** Both tools load instruction/skill files. A rule that must hold in both and must be *enforced* needs adapters for both, not just instructions. Rules that are guidance only can live in instructions alone (D3, D1.9) [DOC H + synthesis].

## Strongest and weakest conclusions

- **Strongest:**
  - Hooks are the vendor-recommended tool for deterministic "always/never" rules (D2.1, D3.1-3).
  - Default failure behaviour differs between Claude Code (proceed) and Copilot CLI `preToolUse` (deny), while timeouts proceed in both (D1.4, D6.3).
  - Copilot has hooks (D4, D6).
- **Medium:**
  - Teams keep trigger lists in data files and ship tests, health checks and kill switches (3 independent repos).
- **Weakest / single-author:**
  - The friction cost of heuristic hooks (X2 cluster ⚠1A).
  - "Over-hooking" (B1 ⚠1A).
  - Exact Windows exit-code rewriting (repo-only claim, I14).
