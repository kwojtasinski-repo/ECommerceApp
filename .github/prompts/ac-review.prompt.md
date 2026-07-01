# AC Review Prompt

> **Usage**: `#file:.github/prompts/ac-review.prompt.md` then append:
> `Review the current Architect Consultant stage implementation.`

---

## Steps

1. Confirm the work claims completion of a specific Blueprint stage.
2. Invoke `@stage-validator` for a Definition of Done check.
3. Invoke `@code-reviewer` (existing agent) for code-quality/anti-pattern review, using
   `.github/templates/REVIEW_TEMPLATE.md` as the output shape.
4. Invoke `@architecture-guardian` if anything in the change looks like it touches the
   frozen architecture baseline.

## Do not

- Do not approve a stage as complete based on code review alone — `@stage-validator`'s
  DONE verdict is mandatory.
- Do not skip `@architecture-guardian` when architecture conformance is in question.
