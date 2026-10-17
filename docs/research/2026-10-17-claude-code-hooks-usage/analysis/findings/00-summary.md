# Summary

- **Question:** How do others use hooks in coding agents (mainly Claude Code), and when is a hook the right mechanism compared with a skill, an instruction or CI?
- **Deliverables:** [findings.md](../../findings.md), [decision-matrix.md](../../decision-matrix.md), [recommendations.md](../../recommendations.md).

## Source count by type (opened this run)

| Type | Count | Items |
|---|---|---|
| Official vendor docs | 5 | D1 Claude hooks reference, D2 Claude hooks guide, D4 VS Code hooks, D5 VS Code Local hooks reference, D6 GitHub Copilot hooks reference |
| Vendor blog | 1 | D3 Anthropic "Steering Claude Code" (2026-06-18) |
| GitHub repos (README) | 3 + 1 pointer list | R1 diet103 showcase, R2 tdd-guard, R3 cc-safety-net; R4 awesome-claude-code |
| Independent blog | 1 | B1 Konishi (2026-06-07) |
| Issues opened in full | 2 | X1 #100891, X2 #100054 |
| Issue titles only | 5 | X3–X7 |
| Internal repo files | 12 | I1–I17 |

## Hypothesis verdicts

| H | Verdict |
|---|---|
| H1 mechanical, costly, always-on, fast → hook | Supported. Use permission rules first for static allow/deny. |
| H2 heuristics with changing triggers → not a hook | Partially. Practice does use heuristic hooks; the pain is trigger lists buried in code. |
| H3 hooks are Claude-only | **Contradicted.** Copilot CLI, Copilot cloud agent and VS Code have hooks; semantics differ. |
| H4 fail-open must be conscious | Supported. It is documented in ADR-0033, and the vendors themselves split. |
| H5 actionable deny messages | Supported. The loop-causation claim was not verified. |
| H6 tests, README, escape hatch, inventory | Supported (3 independent repos plus docs). |
| H7 start-of-prompt token: stable but surprising | Rationale confirmed internally only. |
| H8 instruction → CI → hook | Partially. Permission rules are missing; the CI position was not verified. |

## Strongest / weakest

- **Strongest:**
  - Vendor guidance: deterministic "always/never" rules → hooks or permissions.
  - Default failure behaviour differs by vendor (Claude proceeds on crash; Copilot `preToolUse` denies; both proceed on timeout).
  - Copilot has hooks.
- **Weakest:**
  - Friction cost of heuristic hooks (single author).
  - "Over-hooking" (single author).
  - PowerShell exit 2→1 rewrite (repo-only).
  - CI/pre-commit selection criteria (no source opened).

## Not confirmed

- CI / pre-commit criteria (Q4): docs not fetched.
- A versioning practice for hook rules, beyond committing the config.
- VS Code behaviour of this repo's gate under the current harness model.
- Whether `COPILOT_CLI` is set when VS Code runs the Copilot harness.
- Bodies of issues X3–X7.
