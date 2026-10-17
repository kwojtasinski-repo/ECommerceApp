# Research brief — How coding agents use hooks, and when a hook is the right mechanism

- **Date:** 2026-10-17
- **Skill:** `research-gatherer` (interactive, no `--yolo`)
- **Classification:** `mixed` — internal repo context (read-only) + external sources (official docs, GitHub repos, engineering posts, issue trackers)
- **Status:** Checkpoint 1 approved by human 2026-10-17 ("lecimy"; output in English). Gathering and synthesis done.

## Research question

How do others use hooks in coding agents (mainly Claude Code), and when is a hook the right mechanism
versus a skill, an instruction file, CI, or a pre-commit hook? Conclusions must rest on opened sources,
not on model memory.

## Sub-questions

1. Categories of hook use in practice (security gates, format/lint after edit, context injection, audit/logging,
   notifications, command blocking, prompt validation). Which hook events serve which purpose — verify the
   current event list and exit-code / JSON-output semantics in the official docs.
2. Documented problems: latency, blocking without explanation, loops, fail-open vs fail-closed, flaky hooks,
   debugging difficulty, platform differences (Windows), conflict with other permission mechanisms.
3. Maintenance patterns: hook tests, README, rule versioning, human escape hatch, discoverable hook inventory.
4. When people deliberately choose skill / instruction / CI over a hook, and what criteria they give.
5. Copilot equivalents (agent hooks, custom instructions, pre-commit) and how to combine them so rules apply in both tools.

## User hypotheses to test (stated as opinion, not evidence)

| ID | Hypothesis |
|---|---|
| H1 | Good hook candidate = mechanical, stable rule; high cost of error; must always run regardless of model; fast and deterministic |
| H2 | Bad candidate = heuristics with changing triggers (e.g. "complex LINQ"), anything needing code understanding |
| H3 | Hooks are a Claude Code mechanism; rules that must also bind Copilot belong in instructions/skills |
| H4 | Fail-open vs fail-closed must be a conscious decision; fail-open in the GO-gate adapter may be a weakness |
| H5 | Deny messages must be actionable (say what to do next), otherwise loops occur |
| H6 | Tests + docs in repo, human escape hatch, explicit hook inventory are good practice |
| H7 | Literal start-of-prompt matching is stable but a surprising UX |
| H8 | Default order of choice: instruction/skill → CI → hook (only for fast invariants in Claude Code) |

## Pre-gathering observations from repo context (already read, see `sources.md` §A)

These affect the framing and are flagged for Checkpoint 1:

- **H3 is contradicted by repo evidence.** ADR-0033 states the GO gate runs in Claude Code **and** GitHub Copilot
  (CLI + VS Code) via `.github/hooks/go-gate.json` + `.github/hooks/go-gate-adapter.mjs`, with a tool-agnostic core
  in `tools/hooks/`. `agent-decisions.md` (2026-05-27) records that VS Code Copilot Chat has native hook support
  (Preview) under `.github/hooks/*.json`. Q5 must therefore compare hook *implementations* across tools, not
  assume Copilot has none.
- **H4: fail-open is a documented decision.** ADR-0033 "Fail mode: Fail-open everywhere — a broken hook must never
  wedge agents"; "Fail-closed — rejected: a hook bug would lock every agent out". It also records that Copilot CLI
  preToolUse is fail-closed on non-zero exit, which is why the adapter always exits 0. The research question shifts
  from "is it deliberate?" to "what do sources say about this trade-off for a guardrail (not a wall)?".
- **H7: start-of-prompt matching is deliberate.** ADR-0033 lists "IMPLEMENT at the END is correctly denied" as
  verified behaviour and the reason ("a hook cannot understand intent").

## Scope

**In:** Claude Code hooks (all events), GitHub Copilot hooks (VS Code agent hooks, Copilot CLI, cloud agent),
pre-commit / CI as alternatives, instruction/skill files as alternatives, public repos with real hook configs,
issue trackers for problems and workarounds.

**Out:** Codex, Cursor, Windsurf, Aider and other agents (mentioned only if a source compares them directly);
git server-side hooks; webhook integrations; any change to repo code or config; implementing recommendations.

## Success criteria

- Every claim in `findings.md` has a source URL (or repo path + line), access date, source type
  (doc / repo / blog / issue / opinion) and a reliability rating.
- Official-doc facts, repo practice and opinion are labelled separately.
- Single-author evidence is flagged explicitly.
- Each hypothesis H1–H8 gets a verdict: supported / partially / contradicted / not verified.
- `decision-matrix.md` and `recommendations.md` delivered; recommendations reference this repo's files.
- Closing summary: source counts by type, strongest and weakest conclusions, what could not be confirmed.
