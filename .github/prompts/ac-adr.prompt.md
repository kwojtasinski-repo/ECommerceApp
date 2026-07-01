# AC ADR Prompt

> **Usage**: `#file:.github/prompts/ac-adr.prompt.md` then append:
> `Document <decision> as an ADR.`

---

## Purpose

A thin handoff. This prompt does not generate ADRs itself — it routes Architect
Consultant decisions to the existing `@adr-generator` agent with the right framing.

## Steps

1. Confirm the decision is genuinely ADR-worthy (a significant, durable decision about
   the Architect Consultant framework's own architecture or its implementation approach)
   and not a Stage-local implementation detail.
2. If genuinely ADR-worthy, invoke `@adr-generator` (existing agent) with the decision
   context.
3. If the "decision" is actually a Blueprint-deferred item (see Blueprint §16), do not
   create an ADR — record it as deferred instead.

## Do not

- Do not duplicate `@adr-generator`'s responsibility in this prompt.
- Do not create an ADR for something already covered by the frozen Blueprint/Rules.
