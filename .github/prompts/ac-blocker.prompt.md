# AC Blocker Prompt

> **Usage**: `#file:.github/prompts/ac-blocker.prompt.md` then append:
> `Raise a blocker for <situation>.`

---

## Steps

1. Invoke `@architecture-guardian` to confirm this is genuinely a VIOLATES case, not
   AMBIGUOUS or CONFORMS.
2. If confirmed, create `BLOCKER.md` using `.github/templates/BLOCKER_TEMPLATE.md`.
3. Stop all implementation work on the affected stage.
4. Update `IMPLEMENTATION_STATE.md` (via `@documentation-governance`) to reflect
   `Blocked: Yes` and link the blocker.
5. Wait for human decision. Do not propose workarounds that quietly bend the frozen
   architecture.

## Do not

- Do not resolve the blocker yourself.
- Do not continue unrelated work on the same stage while blocked, unless the human
  explicitly authorizes it.
