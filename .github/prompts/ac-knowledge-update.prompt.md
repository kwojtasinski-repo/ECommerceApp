# AC Knowledge Update Prompt

> **Usage**: `#file:.github/prompts/ac-knowledge-update.prompt.md` then append:
> `Add/update a knowledge library entry for <role>.`

---

## Steps

1. Invoke `@knowledge-librarian` with the target role (one of: archetypes,
   architectural-patterns, heuristics, review-gates, output-contracts).
2. `@knowledge-librarian` must cite the underlying repository evidence (ADR/doc) the
   entry is derived from — no evidence, no entry.
3. Route the actual file change through the normal `@planner` → `@implementer` →
   `@code-reviewer` → `@pr-commit` pipeline. `@knowledge-librarian` proposes; it does
   not commit.

## Do not

- Do not exceed 2–3 seed entries per role without explicit human sign-off.
- Do not add a new front-matter field or a new role — these are deferred decisions.
