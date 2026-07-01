# AC Knowledge Review Prompt

> **Usage**: `#file:.github/prompts/ac-knowledge-review.prompt.md` then append:
> `Review the knowledge library for consistency.`

---

## Steps

1. Invoke `@knowledge-librarian` to audit existing entries against:
   - the 5 frozen roles (no drift into a 6th role)
   - the agreed minimum front-matter fields (no undocumented new fields)
   - the "derived from cited evidence" rule (no orphaned entries without a source citation)
2. Report any drift as a proposal to `@coordinator` — do not silently correct entries
   without going through the normal review pipeline.

## Do not

- Do not treat this as an opportunity to redesign the knowledge library taxonomy.
- Do not add new roles or fields as part of a "review" — that requires a human decision.
