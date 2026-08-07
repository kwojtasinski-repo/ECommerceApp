# Step 1 — Derive the phase breakdown from the ontology

Keep the two halves of this step visibly separate. They differ enormously in cost and reliability,
and collapsing them is how a session talks itself out of Step 2.

← back to [README.md](README.md)

## Derivable from the ontology alone — deterministic and cheap

**Unbuilt labels.** Ontology labels minus the labels observed as built in Step 0.

**Phase ordering, by topological sort over the triples.** An edge cannot be emitted before both its
source and target labels exist, which fixes the order mechanically. This is the single highest-value
thing the ontology gives you: the dependency graph of the build is already written down.

*Worked example (ECommerceApp):* `EXPOSED_BY` needs `Action` ⇒ the Endpoint/Page phase follows the
Action phase. `GOVERNED_BY` needs `Endpoint`/`Page` ⇒ Role/Policy follows that. `SCHEDULES` needs
both `Action` **and** `MessageHandler` ⇒ the Job phase follows the Message phase.

**Each phase's scope.** Every triple touching that phase's labels.

**The leak check.** "No later-phase labels snuck in" is mechanically *all labels outside this
phase* — a generated list, not a judgment call.

**Skeleton validation floors.** Every label the phase emits must end non-zero; every triple emitted
must already be declared in the ontology; an undeclared triple must fail the build rather than
silently widen the schema.

## NOT derivable from the ontology — this is Step 2's entire job

Markers (what in the source identifies an instance of a label), resolution heuristics, edge cases,
and real baseline counts. **The ontology is pure schema.** Say this out loud in the session so
nobody is tempted to skip Step 2 because "the phase list generated itself."

*Worked example (ECommerceApp):* the ontology is 16 labels and 30 triples, and contains not one
word about how an `Endpoint` is recognized in source. The marker — and the fact that most
controllers inherit their marker attribute transitively rather than declaring it — took reading
real controller files, and contradicted what the documentation asserted.

## Splitting a phase

Split a candidate phase into sub-phases when it bundles:

- **unrelated source domains** — different folders, different file kinds, no shared parsing logic;
- **materially different parsing risk** — one part needs alias-splitting, cross-file symbol
  resolution, or inheritance chased across files, while the rest is plain marker presence.

Prefer more, smaller phases when in doubt: a phase that has to be split after implementation has
already cost more than one that was split too early.

*Worked example (ECommerceApp):* two coarse phases became five sub-phases. Role/Policy was split
from Endpoint/Page because alias-splitting is a genuinely different risk profile from attribute
presence. Query/QueryHandler was split from Message/MessageHandler because the two channels have
opposite delivery guarantees and come from different marker interfaces.

## Stop for confirmation

Present the phase count and boundaries as a **draft** and stop. Granularity is a judgment call with
real downstream cost, and it is the human's call — use a closed-set question
(see [verification.md](verification.md) for the host-aware mechanism).

Do not emit any artifact before this confirmation.

## Dry run for this step

Confront the derived order against the state observed in Step 0. If the order says "X before Y" but
Y is built and X is not, that is a contradiction — stop and report it rather than rationalizing it.
