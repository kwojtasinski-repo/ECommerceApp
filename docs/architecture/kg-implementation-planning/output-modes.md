# Steps 3–4 — Output modes

Three modes, one shared discovery pass. Mode is an **output-format switch at the end**, not three
separate pipelines — Steps 0–2 are identical in all of them.

Ask for the mode early so expectations are set, but let it affect only these steps.

← back to [README.md](README.md)

## Mode A — phase artifact pair (default)

Two files, following whatever naming convention Step 0 resolved.

**Implementation artifact** — a summary, not a transcript. Scope · why this phase exists · risk ·
files to modify · files to add · **tests to add** · atomic ordered steps · verification commands ·
risks and open questions · rollback plan.

Keep it short. Do **not** duplicate Step 2's verified-facts material here; the density belongs in
Mode B, and two descriptions of one phase will drift apart.

**Hard rule — the enumerated test list is a contract, not a suggestion.** Every test named in the
implementation artifact's "tests to add" section must, by the time the phase is reported done,
either exist or be declined *explicitly in the report* with a reason. Silently shipping fewer tests
than the plan enumerated is a validation FAIL on its own, regardless of how green the suite is.

This exists because it has happened twice in this repo's own kg-codegen build: Phase 4a shipped 1
test against a plan naming 20, and Phase 4c shipped 0 against a plan naming 9 — both times with a
fully passing suite and a correct emitted graph, and both times caught only by the validator. A
phase's real-graph pins prove *today's* output; the fixtures are what stop a future edit from
regressing the reasoning behind it. Losing them is invisible until it is expensive.

**Validation artifact** — written to be executed by a *different* session:

1. **Deterministic verification** — build, test, codegen check mode; expected counts.
2. **Test-coverage checklist** — one line per test the implementation artifact enumerated, so a
   missing one is a checkbox that cannot be ticked rather than an absence nobody looks for. Include
   at least one regression test proving the heuristic *warns rather than fabricates* when it cannot
   resolve something. For any test whose whole purpose is to catch a specific wrong implementation,
   the validator should **mutate the parser to that wrong implementation and confirm the test fails**
   — a test that passes both ways is decoration. (Phase 4c's first attempt at its highest-value
   fixture passed under the exact bug it was written to catch: edge de-duplication swallowed the
   spurious edge because the fixture reused one target job. Phase 7 repeated it against a live
   database: both depth tests traversed the one fixture chain with no branching, so neither could
   observe a traversal that reported a node once per path length.) The rule these share: **a
   fixture must contain the shape the code can be wrong about**, and the cheapest way to find out
   whether it does is to break the code and watch the test go red.

   For a phase that adds *queries* rather than parsers, two assertions are mandatory beyond the
   happy path, because both failures are silent by construction: what a query returns for an input
   that does not exist, and what it returns for an input of the wrong kind. An empty list for
   either is a defect — it makes a typo indistinguishable from a true negative. Phase 7 shipped
   that defect in nine of ten tools past a green behavioural suite that simply never asked.
3. **Spec-conformance checklist** — every triple declared, coverage ratio reported as a number, no
   later-phase labels leaked (that list is generated in Step 1).
4. **Standard code-review pass** — reuse of existing shared helpers rather than duplicated
   resolution logic; consistent warning discipline; correct ordering of generation stages.
5. **On PASS** — delete only this phase's own artifacts, update the phase-status ledger with the
   real numbers found, and propagate any corrected fact to every place the docs state it.
6. **On FAIL** — delete nothing, report findings, no auto-retry.

It opens by telling the reader not to trust the implementing session's summary and to re-derive
every check from actual repository state.

## Mode B — unattended handoff prompt

One dense, self-contained file for an external agent to execute without supervision. Everything it
needs must be *in* it — it cannot ask follow-up questions.

Structure: a STOP section disambiguating it from unrelated work sharing similar names · governing
docs · prior state verified, with instructions to re-confirm · existing-architecture summary ·
**verified real-code facts with quoted snippets and exact counts** · what to build, with exact
paths · atomic ordered steps · mandatory validation commands with real baselines · a final
self-review checklist.

Grant build/test permission explicitly, and state the one condition for stopping: something turns
out to be wrong when checked against real code.

This mode carries Step 2's findings in full. That is what makes it safe to hand off.

## Mode C — implement here

Build the phase in this session. **Still one phase per pass.**

Then: build, test, run codegen check mode, and compare observed counts against the floors predicted
in Step 1.

**Hard rule:** this mode still emits the Mode A validation artifact, and validation still goes to a
**fresh session**. The implementer must not be the validator. Mode C already spends that
independence on the code; it must not also spend it on the check.

## Choosing

| Situation | Mode |
|---|---|
| planning work for this repo's own review pipeline | A |
| handing a phase to an external or unattended agent | B |
| the phase is small and the human wants it done now | C |

A and B may both be produced for the same phase when it is genuinely being handed off *and*
tracked — but they are generated from one discovery pass, never authored twice.

## Dry run for this step

Re-read the emitted artifact. Every factual claim in it must trace to a Step 2 finding with a
`file:line`. A claim with no source is a hallucination — remove it or go verify it.
