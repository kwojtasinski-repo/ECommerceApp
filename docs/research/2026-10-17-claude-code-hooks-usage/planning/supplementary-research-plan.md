# Supplementary hooks research plan

- Date: 2026-10-17.
- Classification: mixed; additive evidence gathering and verification only.
- Authorization: the current user request explicitly authorizes additional external sources and potentially transferable patterns from other agents. No unattended final synthesis is authorized.
- Baseline: preserve all pre-existing research, including conclusions and source IDs. Use new `S` evidence IDs in supplementary files only.
- Planner: research-planner inspected the baseline before supplementary gathering.

## Research brief

Question: What implementation evidence, independent use cases, and cross-agent patterns are missing from the existing Claude Code and GitHub Copilot hooks research?

Scope in: Claude Code; Copilot VS Code, CLI, and cloud as distinct surfaces; independent hook repositories; official pre-commit and Git documentation; at most two other-agent systems for preliminary portability analysis.

Scope out: modifying local hooks or configuration; executing downloaded hooks; treating documentation as runtime reproduction; replacing Opus's findings; final recommendations or merged synthesis.

## Local hypothesis and discriminating check

The baseline explicitly states that external repositories were inspected at README level only. Implementation inspection may qualify their advertised enforcement, testing, and portability. Check configuration, implementation, and tests together; record a contradiction rather than assuming README claims hold.

## Source manifest and strategy

Candidate URLs are discovery leads, not checked evidence:

- https://github.com/diet103/claude-code-infrastructure-showcase
- https://github.com/nizos/tdd-guard
- https://github.com/kenryu42/claude-code-safety-net
- https://github.com/hesreallyhim/awesome-claude-code
- https://code.claude.com/docs/en/hooks
- https://code.visualstudio.com/docs/copilot/customization/hooks
- https://docs.github.com/en/copilot/reference/hooks-reference
- https://github.com/google-gemini/gemini-cli
- https://pre-commit.com/
- https://git-scm.com/docs/githooks

Gathering agents must record actually opened URLs, short exact quotes, access date, source category, reliability, version or commit where obtainable, and limitations in their own evidence files. Discovery snippets are leads only. Read primary implementation files where accessible. Do not silently substitute package marketing for executable evidence.

## Steps and deliverables

1. Inspect independent hook implementations and discover additional maintainers; write `analysis/findings/supplementary-independent-implementations.md`.
2. Inspect official host semantics, alternatives, and transferable patterns; write `analysis/findings/supplementary-hosts-and-alternatives.md`.
3. Independently verify new evidence against opened sources and baseline claims; write `analysis/findings/supplementary-verification.md`.
4. Leave final synthesis and portability decisions to the supervising agent. Do not modify baseline summaries.

## Success criteria

- Target two independent maintainers beyond baseline R1-R3 and three implementation/test traces; report shortfalls rather than inventing coverage.
- Clearly distinguish vendor documentation, inspected implementation, issue report, author opinion, and local inference.
- Each substantial claim has an opened source, quote or code locator, access date, and limits.
- Host-specific behavior is not generalized to all Copilot surfaces.
- Portability is provisional: identify required events, input/output mapping, missing capabilities, failure mode, and verification still needed.
- Preserve rejected relevant leads, contradictions, inaccessible sources, and untested claims.

## Verification and gates

- Immediately after this plan is added, run a bounded PowerShell existence/content check.
- Validate supplementary Markdown local links, source identifiers, required provenance, and unchanged baseline hashes. These checks validate artifacts, not external runtime behavior.
- Repository RAG refused connections twice (including a lighter retry). Prior-correction lookup used bounded direct-file fallback; no hook implementation changes are planned.
- External pages use available browser/GitHub tools, not the retired context-mode integration.
- Final synthesis gate remains closed. The user requested findings for a separate supervising agent.
- Run the required copilot-setup-maintainer close-out in audit-only mode; do not silently cascade configuration changes.

## Risks and rollback

Moving branches, documentation drift, inaccessible pages, and advertised-but-untested integrations can weaken evidence. Pin revisions when feasible and otherwise label them unpinned. Research rollback consists only of removing this pass's `supplementary-*` additions, never baseline files.