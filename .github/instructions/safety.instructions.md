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
