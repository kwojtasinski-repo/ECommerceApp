# Steps 3–4 — Output modes

Three modes, one shared discovery pass. Mode is an **output-format switch at the end**, not three
separate pipelines — Steps 0–2 are identical in all of them.

Ask for the mode early so expectations are set, but let it affect only these steps.

← back to [README.md](README.md)

## Mode A — phase artifact pair (default)

Two files, following whatever naming convention Step 0 resolved.

**Implementation artifact** — a summary, not a transcript. Scope · why this phase exists · risk ·
files to modify · files to add · atomic ordered steps · verification commands · risks and open
questions · rollback plan.

Keep it short. Do **not** duplicate Step 2's verified-facts material here; the density belongs in
Mode B, and two descriptions of one phase will drift apart.

**Validation artifact** — written to be executed by a *different* session:

1. **Deterministic verification** — build, test, codegen check mode; expected counts.
2. **Test-coverage checklist** — including at least one regression test proving the heuristic
   *warns rather than fabricates* when it cannot resolve something.
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
