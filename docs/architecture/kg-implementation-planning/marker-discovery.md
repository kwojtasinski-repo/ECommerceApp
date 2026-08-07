# Step 2 — Marker discovery against real code

The expensive core. Roughly 80 % of the substance of any phase artifact comes from here, and it is
**identical in every output mode** — never branch on mode before this step is finished.

← back to [README.md](README.md)

## The rule

For the single phase being planned, discover every marker and resolution heuristic by **reading
real source files**. Record `file:line` for each finding.

Documentation is a hypothesis, never a citation — including documentation that says "confirmed".
Re-verify even claims you yourself wrote in an earlier session.

*Worked example (ECommerceApp):* two separate documented "confirmed" claims turned out to be wrong
when finally checked against code — one about which attribute marks an endpoint, one about a
generator emitting nodes it never actually emitted.

## What a finished discovery looks like

For each label in the phase:

- **the marker** — the syntactic or structural signal identifying an instance, with a real example
  quoted from a real file;
- **every branch of that marker** — a marker with two shapes needs both, or one branch silently
  yields nothing;
- **the resolution heuristic** for each edge the phase emits — how a reference in one place is
  matched to a node created somewhere else, and what happens when it cannot be;
- **the failure behavior** — a warning, never a fabricated edge;
- **real baseline counts** — how many instances exist right now, so the phase has a floor to be
  measured against rather than "non-zero".

## Go looking for edge cases

Do not assume a clean 1:1 mapping between documented prose and real code. Every phase attempted so
far has surfaced at least one genuine surprise; assume the next one will too and hunt for it
deliberately. The classes that keep recurring:

| Class | What it looks like |
|---|---|
| multi-marker declaration | one class implementing several marker interfaces at once — collect all, not the first |
| simple-name collision | the same type name in two namespaces, only one of them real — resolve per file, never via a global first-wins index |
| aliasing | an import alias, or a constant standing for several comma-joined real values — resolve to atomic values, never emit a node for the alias |
| dead registration | declared and handled but never actually used — a real finding worth surfacing, not a bug to hide |
| false-positive trap | a different subsystem whose syntax closely resembles the marker |
| declarative blind spot | imperative code doing at runtime what a marker-based parser structurally cannot see |

The last one is never silently under-delivered. Write it into the artifact as a **documented
coverage gap**, with what would be needed to close it.

*Worked examples (ECommerceApp):* four distinct syntactic shapes for one attribute's arguments; a
constant declared separately in two base classes rather than shared; an unrelated caching attribute
whose `PolicyName` parameter mimics an authorization policy; one handler implementing four message
interfaces; two distinct types sharing a simple name, disambiguated per-file; a message with three
handlers that is never published by anything.

## Coverage is a number, not a yes/no

When a heuristic cannot resolve every case, report the actual ratio — `X of Y resolved` — in the
artifact. A ratio below what the plan predicted is a finding that needs an explanation, not a pass
because the count was non-zero.

## Dry run for this step

The real one. Take **2–3 actual instances** from the codebase and hand-trace them through the
proposed marker and resolution heuristic, end to end, before generalizing into a parser design.

If the heuristic breaks on any of them, report it and adjust — that is the step working, not
failing. Report a clean trace just as explicitly.
