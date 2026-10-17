# ADR-0033: GO-Gate Hooks — No File Edits Until the Human Writes IMPLEMENT

## Status
Accepted — guardrail for interactive sessions, known gaps listed in Consequences

## Date
2026-10-14

## Context

Agents started editing source files as soon as they had a plan. The wanted behaviour: no file edits
until the human explicitly approves implementation (uppercase token `IMPLEMENT`; `APPROVE` alone only
accepts a plan), and once GO is given, implementer, validator and research agents never block each other.
It has to work in Claude Code and GitHub Copilot (CLI + VS Code). Codex is not wired in this repo and is out of scope.

The design is ported unchanged in structure from the sibling Gradebook repository (its ADR-010), where it
was verified live on all three surfaces on 2026-10-13. The findings below are those verified findings;
they are requirements, not suggestions. Claude Code and Copilot CLI were then exercised live in this repo
on 2026-10-14 (see "Verified live in ECommerceApp"); VS Code was not re-tested here.

## Decision

One tool-agnostic core plus thin per-tool adapters. The core knows no tool names, so it lives in
`tools/hooks/`; every adapter lives in its own tool folder and only maps field names and output shape.

```
user prompt --> adapter --> core.evaluate(prompt) : start-of-prompt token match --> marker(session) written
agent asks the user, user says yes --> agent runs `node tools/hooks/gate-cli.mjs grant <sessionId>`
any agent / subagent --> edit tool --> adapter --> core.evaluate(edit)
        CI / cloud env?  --> allow         marker valid?  --> allow
        every path allow-listed? --> allow
        otherwise DENY with a reason naming the grant command
core / adapter error, bad payload --> ALLOW (fail-open, logged to <tmp>/ecommerceapp-go-gate/errors.log)
```

| Choice | Decision | Reason |
|---|---|---|
| Layout | `tools/hooks/` core + `.claude/hooks/`, `.github/hooks/` adapters | One behaviour, tool-agnostic rule intact, each tool keeps self-contained files |
| Scope | Edit tools only; shell not gated | Guardrail, not a wall: `sed -i` or redirects bypass it by design. Gating shell would block builds/tests and agents |
| Agents the user launches | Planning, research and validation agents write only to allow-listed folders (`.github/plans/`, `docs/research/`, `docs/reports/`), reviewers are read-only, so none are blocked before GO. Only source edits wait for `IMPLEMENT`. Plan Mode approval does not grant | The gate targets an agent that starts coding on its own, not workflows the user started |
| Subagents | Not detected. Session-scoped GO marker shared by everyone in the session | A shared marker needs no detection and means agents never block each other |
| Consent token | Case-sensitive uppercase `IMPLEMENT` at the start of the prompt (optionally after `APPROVE`); revoke `HOLD`/`STOP`. `APPROVE` alone does not unlock | A hook cannot understand intent. Lowercase prose must never unlock. Plan acceptance and permission to edit are different decisions |
| Marker location | `<os tmp>/ecommerceapp-go-gate/<sessionId>.go`, TTL 12 h, directory excluded from the temp-dir allowance | Otherwise an agent could write its own GO marker |
| Fail mode | Fail-open everywhere | A broken hook must never wedge agents |
| CI / cloud | Self-disabling when `CI` / `GITHUB_ACTIONS` is set; `ECOMMERCEAPP_GATE=off` switches it off locally | Copilot cloud agent reads `.github/hooks/*.json` and has no human to say GO |
| Dialog answer | A question-tool answer is not a prompt, so the agent runs `gate-cli.mjs grant <sessionId>`; the deny reason names the exact command | Keeps one marker mechanism for prompts and dialogs |
| Question tools | Claude `AskUserQuestion`; VS Code `#vscode/askQuestions`; Copilot CLI `ask_user`. Plain-text question is the contract when none exists | Codex `request_user_input` intentionally dropped (Codex out of scope) |

### Findings that shaped the implementation (verified live in Gradebook, 2026-10-13)

- **PowerShell rewrites hook exit code 2 to 1**, which VS Code treats as a non-blocking warning. A deny
  therefore travels as JSON on stdout with exit 0 (Claude: `hookSpecificOutput`; Copilot: both the
  top-level and the `hookSpecificOutput` shape).
- **Relative hook paths drift** after an agent `cd`s into a subfolder (gate silently off). Commands resolve
  the adapter from `CLAUDE_PROJECT_DIR` or by walking up from the hook cwd, through `pathToFileURL`.
- **Copilot CLI preToolUse is fail-closed on a non-zero exit**, including a launcher that fails to start.
  The launcher is a single-string command and the adapter always exits 0.
- **Copilot CLI also loads `.claude/settings.json`** as "repo settings" and sends Claude-shaped payloads
  with a string `tool_input`; the Claude adapter stands down when `COPILOT_CLI` is set.
- **Copilot CLI subagents get their own `sessionId` with no link to the parent**, so all CLI sessions in a
  repo share one marker key (`copilot-cli-<hash of repo root>`). Trade-off: concurrent CLI sessions in the
  same repo share GO until `HOLD`/`STOP` or the TTL. A `sessionStart` event revokes a marker older than 10 s
  so a fresh session starts locked; that revoke is covered by unit tests only.
- **`copilot -p` (non-interactive) does not load repo hooks** (CLI 1.0.97). The gate covers interactive sessions only.
- **Copilot hook timeouts fail open** (documentation, checked 2026-10-17): a slow or unreachable hook does not
  block the tool call, consistent with this gate's fail-open decision.
- **Autopilot auto-answers `ask_user`**, so a dialog in autopilot never yields a real GO. The gate holds;
  the agent stops and reports. Intended safe outcome.
- **VS Code**: `chat.useHooks` is on by default (Local harness); Claude-format files additionally need
  `chat.useClaudeHooks` (off by default). It ignores matchers in Claude-format files (the core filters on tool
  name itself) and reuses the parent `session_id` in subagents.
- **Agents can self-grant.** The `grant` command exists for dialog approval, so an agent can run it without
  real approval. The transcript shows it and the deny reason says to run it only after explicit user approval.

### VS Code documentation clarification (2026-10-17)

The VS Code documentation states that `chat.useHooks` controls hook execution in the **Local
harness** and is on by default; it is not an additional opt-in unless disabled. Discovering Claude-format
hooks from `.claude/settings.json` additionally requires `chat.useClaudeHooks`, which is off by default.
VS Code also offers Copilot and Claude harnesses with different hook implementations, so these setting
defaults do not establish that this gate was re-tested or works in every harness. The ECommerceApp VS Code
gate remains **not re-tested**; a follow-up probe must name the target harness.

Source: [Configure agent hooks in Visual Studio Code](https://code.visualstudio.com/docs/agent-customization/hooks),
checked 2026-10-17. Documentation states `chat.useHooks` is on by default and
`chat.useClaudeHooks` is off by default.

### Verified live in ECommerceApp (2026-10-14, user-run)

| Surface | Result |
|---|---|
| Claude Code | Edit denied before GO; allowed after `IMPLEMENT`; subagent edit allowed after GO; `HOLD` re-locks. A prompt with `IMPLEMENT` at the END (task text first) is correctly denied — the token must start the prompt. A write to `.github/plans/` succeeded, but only after GO, so the pre-GO allow-list was not observed live. Shell (`rm`) is not gated, as designed. Hooks added to `.claude/settings.json` mid-session took effect in an already-running session |
| Copilot CLI (interactive, autopilot) | Gate behaved: denied before GO with the hook message, allowed after `IMPLEMENT`, and a general-purpose subagent edit succeeded after GO (shared repo-hash marker) |
| VS Code Copilot | Not re-tested here |

### Copilot: two independent permission layers

The GO gate is one layer. Copilot CLI has its own permission policy, and **both must allow** a tool call.
`IMPLEMENT` only satisfies the gate; it never approves Copilot's own permission prompt.

How to tell them apart in a transcript:

- `Denied by preToolUse hook: Edits are locked until the user writes IMPLEMENT…` — the GO gate. Fix: start the prompt with `IMPLEMENT`.
- `Permission denied because no interactive user response was available` / `Approval check ✗ Denied, no one to approve …` — Copilot's permission policy (for example `Manual Approval`). In autopilot nobody can answer, so every write and shell command that needs approval is auto-denied, gate or no gate. The gate is not at fault.

Options for autopilot:

| Option | Effect | Trade-off |
|---|---|---|
| Interactive mode | Copilot asks, the human approves each write/command | Safest; defeats the point of autopilot |
| Narrow allow rules (write tool, `git`, `dotnet`, `node`) via `/permissions` or `--allow-tool` | Autopilot runs without prompts for those; GO gate still holds file edits | Recommended. Check exact syntax for your CLI version |
| Allow all tools | No Copilot prompts at all; the gate is the only guard | An agent can then run `gate-cli.mjs grant` itself or edit through the shell. Consider removing the `grant` hint from the deny message or denying `node tools/hooks/gate-cli.mjs grant*` |

Do not make the gate approve permissions for Copilot: that would turn every `IMPLEMENT` into a blanket
shell approval. In autopilot `ask_user` is auto-answered ("best guess"), so a dialog answer is never real consent.

## Consequences

### Positive
- Source edits wait for an explicit human `IMPLEMENT`; planning, research and review flows are not blocked.
- One behavioural core with unit tests; adapters are thin and independently tested.

### Negative
- A guardrail, not a wall: shell edits, `copilot -p` and self-granting agents bypass it.
- The Copilot CLI marker is shared by concurrent sessions of one repo.

### Risks & mitigations
- Hook bug locks agents out: mitigated by fail-open everywhere and `ECOMMERCEAPP_GATE=off`.
- VS Code behaviour remains not re-tested in this repo; Gradebook findings are not assumed to transfer, and any follow-up probe must name the harness (Local, Copilot or Claude).
- Hook regressions: a CI job (`hook-tests` in `.github/workflows/dotnet-ci.yml`) runs the hook unit tests (added 2026-10-17; workflow is currently manual-trigger only).
- Autopilot users who see "no one to approve" assume the gate is broken: documented in "Copilot: two independent permission layers".

## Alternatives considered

- **One literal script shared by all tools** — rejected: payload shapes and output contracts differ per tool; a single file became a tangle of branches.
- **Detecting subagents to exempt them** — rejected: fragile outside Claude.
- **Prompt-only rule in agent instructions** — rejected: not enforced; the original problem.
- **Gating shell commands** — rejected: blocks builds and tests, high false-positive rate, still bypassable.
- **Fail-closed** — rejected: a hook bug would lock every agent out of the repo.
- **Codex adapter** — out of scope: Codex is not wired in this repo.

## Migration plan

N/A

## Conformance checklist

- [ ] `tools/hooks/gate-core.mjs` contains no tool names; adapters only map payloads and output
- [ ] Every hook entry point exits 0 on internal error (fail-open) and logs to `errors.log`
- [ ] Deny travels as JSON on stdout with exit 0, never exit code 2
- [ ] Allow-listed globs stay limited to `.github/plans/**`, `docs/research/**`, `docs/reports/**`
- [ ] Marker directory is excluded from the temp-dir write allowance

## References

- Tests (pass explicit file paths, not a directory):
  `node --test tools/hooks/gate-core.test.mjs .claude/hooks/go-gate-adapter.test.mjs .github/hooks/go-gate-adapter.test.mjs`
- Source design: Gradebook `agents/decisions/010-go-gate-hooks.md`
- `.github/AGENT-PIPELINE.md` — "GO gate" section
- `tools/hooks/README.md` — per-tool permission guide; the deny message points agents to it
