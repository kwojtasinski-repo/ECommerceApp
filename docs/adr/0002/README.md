# ADR-0002: Post-Event-Storming Architectural Evolution Strategy

**Status**: Accepted
**BC**: All (migration strategy)

## What this decision covers
Parallel Change strategy, BC isolation rules, atomic switch policy,
and the 80–95% completion gate before atomic switches.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0002-post-event-storming-architectural-evolution-strategy.md | Migration strategy, BC isolation rules, switch policy | Before editing any BC boundary or migration sequencing |

## Key rules
- Atomic switches deferred until 80–95% of BC implementations complete
- Legacy code untouched until switch — parallel change only
- MUST read project-state.md before any BC edit

## Related ADRs
- ADR-0003 (folder structure) — implements this strategy
