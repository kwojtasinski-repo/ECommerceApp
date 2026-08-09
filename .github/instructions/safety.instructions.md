---
applyTo: "**"
---

# Safety — Allowed / Disallowed Actions

## Allowed (without separate approval)

- Small, focused code changes and refactors requested by humans that include tests and pass CI.
- Add or update documentation, ADRs, and test-only helper code.
- Non-destructive CI and doc changes.

## Disallowed without explicit human approval

- Any edits to files under `Infrastructure/Migrations/` or running production DB migrations.
- Introducing or committing secrets or credentials.
- Upgrading to preview SDKs or preview major package versions.
- Large API-breaking changes, cross-service contract changes, or mass refactors without an accepted ADR and sign-off.
- Any automated change that directly affects production systems.
- Never assume or hard-code framework or package versions — ask the human for confirmation.
- Do not perform destructive actions or operations against production systems.

## External HTTP / URL fetching

Per [mcp-routing.instructions.md](mcp-routing.instructions.md) rule #3:

- **For any project-related URL** (ADR references, docs, GitHub links, package registries cited in repo work) → use the approved project URL retrieval path. **Never** route through the retired context-mode integration.
- **Carve-out**: non-project URLs the user explicitly asks you to read in raw form (e.g. "read this random blog post for me") may use direct `fetch_webpage`, but say so in the answer.
- Use raw `fetch_webpage` sparingly and only for clearly necessary non-project lookups; never for content already in the indexed `docs/`.

## MCP tool restrictions

- **`@verifier`** (and any deterministic verification step) MUST NOT call MCP tools — see `agents/verifier.md`.
- **NEVER call unrelated MCP families for the same atomic intent.**
