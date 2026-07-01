# REVIEW_TEMPLATE

> Used alongside `@code-reviewer` (existing agent) when reviewing work against the
> Architect Consultant Playbook's Code Review Checklist. This template structures the
> output; it does not replace `@code-reviewer`'s own judgment process.

## Stage Under Review

- Stage name and number

## Checklist (per IMPLEMENTATION_PLAYBOOK.md — Code Review Checklist)

- [ ] The work matches the current stage objective
- [ ] The work stayed within the frozen architecture
- [ ] Definition of Ready was satisfied before implementation started
- [ ] Definition of Done was verified before completion
- [ ] The output is traceable to evidence or explicit gaps
- [ ] No forbidden abstractions were introduced
- [ ] No scope expansion occurred
- [ ] Risks and remaining work were documented
- [ ] Blockers were raised immediately when needed
- [ ] The implementation remains understandable for the next stage

## Findings

- <finding> — <severity: blocks merge / advisory>

## Verdict

- Approved / Changes Requested / Blocked (route to `@architecture-guardian` if
  architecture conformance is in question)
