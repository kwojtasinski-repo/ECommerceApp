# AC Stage Complete Prompt

> **Usage**: `#file:.github/prompts/ac-stage-complete.prompt.md` then append:
> `Mark Stage <N> complete.`

---

## Steps

1. Invoke `@stage-validator` for a Definition of Done check on Stage `<N>`.
2. If verdict is `DONE`:
   - Invoke `@documentation-governance` to update `IMPLEMENTATION_STATE.md` using
     `.github/templates/STAGE_REPORT_TEMPLATE.md` as the report shape.
   - Report the next recommended stage per the Blueprint's delivery order.
3. If verdict is `NOT DONE`:
   - Do not update `IMPLEMENTATION_STATE.md` to complete.
   - Report the itemized gaps and stop.

## Do not

- Do not mark a stage complete on an implementer's self-report alone.
- Do not proceed to the next stage before this stage is confirmed `DONE`.
