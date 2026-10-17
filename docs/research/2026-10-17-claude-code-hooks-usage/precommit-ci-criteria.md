# Pre-commit / CI vs agent hook vs instruction: selection criteria

Gap filled: findings.md Q4 row "CI / pre-commit instead of a hook" was "Not verified". Accessed 2026-10-17.
Labels: DOC = official documentation; PRACTICE = third-party blog/guide describing real use; OPINION = author's view; UNVERIFIED = not confirmed by an opened source.

## 1. What each layer guarantees

| Claim | Label | Source |
|---|---|---|
| Git `pre-commit` "can be bypassed with the `--no-verify` option". | DOC | https://git-scm.com/docs/githooks |
| pre-commit (the tool) documents `SKIP=hook-id` for skipping a single hook "instead of `--no-verify`ing the entire commit"; so skipping is a supported, normal path. | DOC | https://pre-commit.com/ |
| Server-side git hooks exist: `pre-receive` exit non-zero means "none of the refs will be updated"; `update` can reject per ref. These run on the remote, not on the developer's machine. | DOC | https://git-scm.com/docs/githooks |
| Git hooks live in `$GIT_DIR/hooks` (or `core.hooksPath`). The page does not say hooks are "not cloned"; that is an inference. Hooks need per-clone installation. | DOC + inference (UNVERIFIED as a stated fact) | https://git-scm.com/docs/githooks |
| pre-commit docs recommend `pre-commit run --all-files` as a CI step, so the same config can be enforced in CI. | DOC | https://pre-commit.com/ |
| pre-commit warns that the first run of a hook may be slow, and that running hooks on unstaged changes gives false positives/negatives. | DOC | https://pre-commit.com/ |
| GitHub required status checks must be successful/skipped/neutral before changes reach a protected branch; even people with push permission cannot merge when required checks fail. | DOC | https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches |
| By default branch-protection restrictions "don't apply to people with admin permissions"; admins are bound only with "Do not allow bypassing the above settings". So CI is binding on everyone only if configured that way. | DOC | same page |
| Azure DevOps: build validation / status checks are branch policies; "all changes must go through pull requests" on a branch with required policies. The "exempt from policy" permission was NOT confirmed in the opened text. | DOC (partial), bypass part UNVERIFIED | https://learn.microsoft.com/en-us/azure/devops/repos/git/branch-policies |
| Claude Code hooks give "deterministic control: certain actions always happen rather than relying on the LLM". They fire only inside a Claude Code session (by definition of lifecycle events). | DOC | https://code.claude.com/docs/en/hooks-guide |
| A `PreToolUse` `deny` blocks the tool even in `bypassPermissions` / `--dangerously-skip-permissions`: "enforce policy that users can't bypass by changing their permission mode". Hooks can tighten, not loosen, permission rules. | DOC | https://code.claude.com/docs/en/hooks-guide |
| Copilot hooks: `preToolUse` "can approve or deny tool executions"; available in cloud agent. The page makes no comparison with git hooks or CI. | DOC | https://docs.github.com/en/copilot/concepts/agents/cloud-agent/about-hooks |
| An agent can defeat a client-side git hook with `--no-verify` or `git stash`; a cited claude-code issue (Opus 4.6 bypassing deny rules and CLAUDE.md across commits) was closed "not planned". Secondary report, issue itself not opened. | PRACTICE (issue UNVERIFIED) | https://pydevtools.com/handbook/how-to/how-to-stop-ai-agents-from-bypassing-pre-commit-hooks/ |
| "The agent cannot pass `--no-verify` to CI." | PRACTICE/OPINION | pydevtools guide above |

Derived properties (synthesis from the rows above):

| Property | Agent hook (Claude/Copilot) | pre-commit (git, local) | CI + required check |
|---|---|---|---|
| Binds the agent | Yes, at tool call (while the hook is configured and the session uses it) | Only if the agent commits and does not use `--no-verify` | Yes, at push/PR, cannot be skipped by the pusher |
| Binds humans | No | Yes locally, skippable | Yes (admins only if configured) |
| Sees agent edits that are never committed | Yes (acts on the edit) | No | No |
| Server-side | No | No | Yes |
| Speed budget | Per tool call, synchronous | Seconds (see section 2) | Minutes OK |
| Needs per-clone install | Config is committed, tool reads it | Yes (`pre-commit install`) | No |

## 2. Explicit criteria: pre-commit vs CI

| Criterion | Label | Source |
|---|---|---|
| Split by speed: "everything that takes milliseconds ... runs in pre-commit and everything that is slower runs in the CI"; pre-commit "unsuitable for running slow tasks (slower than a second or two)". | OPINION (single author) | https://switowski.com/blog/pre-commit-vs-ci/ |
| Recommends CI even with pre-commit, because pre-commit relies on each person installing it; small/solo projects may start with pre-commit only. | OPINION | same |
| Pre-commit = fast, deterministic, changed-file-scoped (format, lint, secrets, targeted static analysis). CI = full test suites, full static analysis/SCA, build validation, compliance evidence, release gates. | PRACTICE (vendor blog, Codacy, commercial interest) | https://blog.codacy.com/pre-commit-vs-ci-quality-gates-when-fast-shipping-moves-the-checks-upstream |
| Slow pre-commit gets bypassed: "Developers will find a way around it within days if it slows them down." Share a severity model between local and CI; allow explicit override for warnings. Move a check earlier only if late feedback is costly. | PRACTICE/OPINION | same |
| Hooks are skippable, so secret scanning needs server-side enforcement plus required check. | PRACTICE | search summary (aicodereview.cc, xygeni.io; pages not individually opened, UNVERIFIED in detail) |

## 3. Agent hook vs pre-commit/CI vs instruction

No official doc (Claude Code, Copilot, pre-commit, git, GitHub, Azure DevOps) gives a decision rule comparing agent hooks with git hooks/CI. Copilot hooks page and git docs make no such comparison (DOC, checked). The criteria below are therefore synthesis from the rows above, plus one PRACTICE guide.

| Question | If yes, prefer | Basis |
|---|---|---|
| Must the rule bind humans or other tools/IDEs, or any contributor who never runs Claude/Copilot? | CI with required check (pre-commit as early feedback) | Hooks fire only in agent sessions; CI is server-side (DOC) |
| Must the rule hold even if the agent edits but never commits, or must it stop an action before it happens (edit lock, dangerous command, GO gate)? | Agent hook (`PreToolUse`) | Git hooks see only commits; hook deny survives bypassPermissions (DOC) |
| Is the check slow (over ~2 s) or needs full repo/environment? | CI | Section 2 (OPINION/PRACTICE) |
| Fast, deterministic, changed-file check (format, lint, secrets)? | pre-commit locally and the same config in CI | Section 2; `pre-commit run --all-files` in CI (DOC) |
| Is it a tool+argument allow/deny? | Permission rules; hook only when the rule needs code | DOC (hooks-guide: `if` filter is best-effort, use permissions for hard allow/deny) |
| Is the miss cheap, or does the rule need judgment? | Instruction/skill (or prompt hook) | prior findings Q4 |
| Is the agent expected to bypass a client-side gate (`--no-verify`)? | Add a `PreToolUse` hook that rejects bypass flags, with CI as backstop | PRACTICE (pydevtools) |

## 4. Layering order and where permission rules fit

PRACTICE (single guide, https://pydevtools.com/handbook/how-to/how-to-stop-ai-agents-from-bypassing-pre-commit-hooks/), weakest to strongest:

1. CLAUDE.md instruction: documents the rule, "does not stop a determined agent".
2. `permissions.deny`: "free partial backstop"; misses flags placed after the message (`-m "wip" --no-verify`).
3. `PreToolUse` hook parsing the full command: "the only [local layer] that reliably enforces the rule" for Claude Code.
4. PATH-shim git wrapper for any shell agent; bypassable by calling `/usr/bin/git` directly.
5. pre-commit in CI as the final backstop.

Corroborating DOC: permission rules match the command text Claude writes and are "not a security boundary around the program"; use sandboxing for enforcement independent of command text, or a `PreToolUse` hook to inspect the full command (https://code.claude.com/docs/en/permissions). Hooks run before permission-mode checks and can tighten but not loosen rules (hooks-guide).

Suggested stack (synthesis, not from a single source): instruction/skill for guidance; permission rules for hard tool/argument patterns; agent hook for stateful or code-needing invariants and for blocking bypass flags; pre-commit for fast local feedback; CI + required status check (with admin bypass disabled) as the only layer that binds everyone and cannot be skipped by the pusher.

## 5. Not verified

- Azure DevOps "exempt from policy enforcement" permission (not found in fetched text).
- That git hooks are not cloned (inference only; docs silent).
- The claude-code GitHub issue about `--no-verify` bypass (reported second-hand).
- aicodereview.cc, xygeni.io, Codacy-adjacent statistics, and the wshobson block-no-verify skill were seen only via search snippets (skill itself says it had no security audit).
- Whether Copilot cloud agent PRs trigger required CI checks (Copilot hooks page silent).
- Speed thresholds ("1-2 s") are one author's opinion, not a standard.
- Windows-specific behaviour of these hooks in this repo not tested.
