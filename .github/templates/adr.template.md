# ADR-XXXX: [Short title — imperative, e.g. "Use Handler pattern for complex domain operations"]

## Status
<!-- Choose one: Proposed | Accepted | Deprecated | Superseded by ADR-XXXX -->
Proposed

## Date
<!--
Always resolve today's date from your local environment before filling this field.
Run the appropriate command for your shell:
  PowerShell : Get-Date -Format "yyyy-MM-dd"
  bash/zsh   : date +%F
Never hardcode or guess the date. Format must be: YYYY-MM-DD
-->
YYYY-MM-DD

## Context
<!--
Describe the situation, problem, or opportunity that triggered this decision.
Include relevant constraints, business requirements, and technical context.
What forces are at play? What is the current pain point?
-->

## Decision
<!--
State the decision clearly and concisely.
Use active voice: "We will use X because Y."
-->

## Consequences
<!--
What becomes easier or harder as a result of this decision?
Include both positive and negative consequences.
-->

### Positive
-

### Negative
-

### Risks & mitigations
-

## Alternatives considered
<!--
List the alternatives that were evaluated and why they were rejected.
-->

- **Option A** — [description] — rejected because [reason]
- **Option B** — [description] — rejected because [reason]

## Migration plan
<!--
If this decision requires changes to existing code, infrastructure, or process,
describe the steps needed to migrate. If no migration is needed, write "N/A".
-->

N/A

## Conformance checklist
<!--
List the structural invariants that can be verified mechanically during PR review.
Keep these as WHAT to check (invariants), not HOW to check (tool commands).
Build and test verification belongs in the agent/reviewer process, not in the ADR.

Examples:
  - [ ] All aggregate properties use `private set`
  - [ ] Static `Create(...)` factory method present, returns `(Aggregate, DomainEvent)`
  - [ ] Aggregate files live under `Domain/<Group>/<BcName>/`
  - [ ] No cross-BC navigation properties — IDs only
  - [ ] `DbContext` uses schema `"<schema>"`
  - [ ] Service implementation is `internal sealed`
-->

- [ ]

## References
<!--
Links to relevant tickets, docs, RFCs, related ADRs, or external resources.
-->

- Related ADRs: <!-- ADR-XXXX -->
- Issues / PRs: <!-- link -->
- Repository: https://github.com/kwojtasinski-repo/ECommerceApp

## Reviewers
<!-- Tag architecture owners or maintainers required to approve this ADR. -->

- @team/architecture
