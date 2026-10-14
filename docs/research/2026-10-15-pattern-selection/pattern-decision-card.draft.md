# Pattern decision card (DRAFT: research artifact, not loaded by any agent)

Default: NO pattern. Skip this card for: <=2 files, clear spec, reversible, known fix.
Choose only from this closed list. Say one line: "Pattern: X (why; cost S/M/L)". Never invent patterns.
Always: rounds <=2, workers <=4 (A/B), stop when a round adds no new verified evidence. Tier C = ask the user first.
If estimated cost > ~10% of remaining budget: go up one tier or take the cheaper variant.

| Signal | Pattern | Tier | Do not use when |
|---|---|---|---|
| Fixed order of steps | Sequential with gates (default) | A | - |
| >=3 independent sub-questions, little shared state | Fan-out (read-only) + decomposition check | A | edits on shared files, sequential or tool-heavy work |
| Steps unknown, may need re-plan | Planner -> Executor | B | clear spec (just do it); stop after 2 re-plans and ask |
| High blast radius | Deterministic checks first, then independent validation (other session/model) | B | low-risk or reversible change |
| Contested claim behind a costly decision | One critic that verifies sources | A | nothing contested |
| >=2 viable alternatives, no deterministic check, costly error | Debate (positions, critiques, judge; enum verdict) | C | single answer exists; same-model-only panel |
| Binary high-stake call | Quorum of 3 independent voters | C | open-ended quality questions |
| State shared across models/sessions | Blackboard files (one file per writer, append-only, entries marked resolved) | A/B | no second consumer |
| Exploratory read-only diagnosis, path unknown | Swarm / handoff, read-only | C | any edit |
| Need a specialist team | Pick from the registry (closed list, <=4, user approves in plan) | B | a fixed delegation map already fits |
| - | Auction, free-form event automation | - | skip |
