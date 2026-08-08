# KG implementation planning — methodology

Source of truth for **how** a confirmed knowledge-graph ontology gets turned into a phased,
verifiable build of the codegen tool that populates it.

Two thin wrapper skills point here instead of duplicating this content:
- Claude Code: `.claude/skills/kg-implementation-planner/SKILL.md`
- GitHub Copilot: `.github/skills/kg-implementation-planner/SKILL.md`

This is the companion to [`../knowledge-graph-ontology-design.md`](../knowledge-graph-ontology-design.md),
which covers designing **what** belongs in the graph and explicitly stops before implementation.
That doc's own "When not to use" boundary is this doc's starting line.

## Deliberately project-agnostic

Nothing here assumes a language, build system, folder layout, VCS workflow, or agent tool API.
Concrete ECommerceApp/`kg-codegen` details appear only as **worked examples**, always marked as
such. When applying this to another project, the examples illustrate the shape of a finding — they
are never the finding itself.

## Preconditions

### The ontology file must be pointed at explicitly

The human supplies the path to the ontology produced by the ontology-design skill — as an argument
or as the first question asked. The skill may *offer* discovered candidates to choose from, but it
never picks one silently: the ontology is the spec everything downstream derives from, and a wrong
pick poisons every later step invisibly.

Also confirm the ontology has been through the design skill's mandatory dry run. An unvalidated
draft is not a valid input.

No confirmed ontology file → **stop** and send the human to the ontology-design skill. This skill
never designs or edits an ontology; it reinforces that boundary from the other side.

### Irreducible assumptions

Everything else is discovered at runtime (see [project-profile.md](project-profile.md)). These
three cannot be:

1. The ontology declares **node labels** and **relationship triples** — `(source label, type,
   target label)`. Nothing beyond that shape is assumed.
2. A **codegen tool exists or is being built**, with a check/dry-run mode reporting per-label node
   counts. Without it there is nothing to verify a phase against.
3. A **VCS is present** and its working-tree state is readable.

## The flow

Each step delegates to its own file. Read the step you are on; do not preload all of them.

| Step | What happens | Detail |
|---|---|---|
| 0 | Resolve the project profile; reconcile every independent signal of what is already built | [project-profile.md](project-profile.md) |
| 1 | Derive phases from the ontology's triples; stop for confirmation of boundaries | [phase-derivation.md](phase-derivation.md) |
| 2 | Discover markers and edge cases against **real code** — the expensive, mode-independent core | [marker-discovery.md](marker-discovery.md) |
| 3 | Emit in the chosen output mode | [output-modes.md](output-modes.md) |
| 4 | Execute (implement-here mode only), then hand validation to a fresh session | [output-modes.md](output-modes.md) |

Verification is not a step — it runs *after every step*. So do the stop-and-ask triggers and the
host-aware asking rules: [verification.md](verification.md).

## Two rules that outrank everything else

**One phase per pass.** Never plan, prompt for, or build a phase whose inputs depend on an earlier
phase that is not both implemented **and** validated. Context budget and verification quality both
degrade when several phases are pushed through in one sitting.

**Validation stays independent of implementation, in every mode.** The validator must not be the
implementer. This is the property the whole pipeline buys; the implement-here mode already spends
it on the code, so it must not also spend it on the check.

## Knowledge durability

A fact corrected during Step 2 must be propagated to **every** place the documentation states it,
not just the first one found — and never left only in a phase artifact, because those are deleted
on validation PASS by convention. Ephemeral artifacts are not a place to store durable knowledge.

*Worked example (ECommerceApp):* the `Endpoint` marker was corrected in the ontology section of the
design doc while the same disproved claim survived in the phase list 150 lines later, with the full
correction living only in a plan file scheduled for deletion.

The same principle applies to **numbers**, which decay differently from claims: a count is
plausible forever, so nothing ever flags it as stale. Every count a phase publishes therefore ships
with the command that regenerates it and a statement of what population it counts — otherwise it
can only be copied forward, never confirmed or retired. See
[verification.md](verification.md#a-published-number-carries-its-reproduction-command).

## Worked example

ECommerceApp's `kg-codegen` — a 9-phase build (0–1, 2, 3a, 3b, 4a, 4b, 4c, 5, 6, 7) driven from a
16-label / 30-triple ontology. See the design doc's "Implementation plan" section for the phase
list and per-phase outcomes. Grounding and illustration only.
