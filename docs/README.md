# ECommerceApp Docs

Short human entry point for the `docs/` folder.

## What is here

- `adr/` — architecture decisions, organized as `docs/adr/<NNNN>/`
- `architecture/` — bounded-context map and structural views of the system
- `patterns/` — reusable implementation guidance
- `reference/` — endpoint maps and other lookup material
- `reports/` — analysis snapshots and generated status reports
- `roadmap/` — planned work, sequencing, and migration tracks

## Recommended reading order

1. `adr/0001/README.md` for project-wide architectural context
2. `architecture/bounded-context-map.md` for BC ownership and status
3. `roadmap/README.md` for current sequencing and blockers

## Docs vs. Copilot environment

- `docs/` is the human-facing knowledge base.
- `.github/instructions/docs-index.instructions.md` is the Copilot-facing routing table.
- When a docs change affects meaning, navigation, workflow, or architecture, the `.github/` Copilot environment should be updated too.
