# Step 0 — Project profile + state reconciliation

Two jobs: work out *where things are* in this project, and work out *what is already built*. Both
by discovery and asking, never by assumption.

← back to [README.md](README.md)

## Part A — Resolve the profile

Resolve each of these once per session, ask when ambiguous, and keep the answers stable for the
rest of the session:

| What | How resolved | Never |
|---|---|---|
| ontology file | **supplied explicitly by the human** (see README preconditions) | glob-and-pick silently |
| phase-artifact directory | detect an existing one; none → ask | assume a conventional path |
| phase-file naming convention | infer from files already there; none → propose and confirm | impose a naming scheme |
| build + test commands | read the project manifest (`*.sln`, `package.json`, `Cargo.toml`, `pyproject.toml`, `go.mod`, …) | guess from language alone |
| codegen entry point + its check-mode invocation | discover; not found → ask | invent a flag name |
| phase-status ledger (where "done" is recorded) | ask once | scatter status across artifacts |

Record the resolved profile explicitly in the session so later steps quote it rather than
re-deriving it.

*Worked example (ECommerceApp):* ontology at `tools/kg/seed/ontology.json`; artifacts in
`.github/plans/` named `NN-phase-<slug>-{implementation,validation}.md`; `dotnet build` / `dotnet
test`; check mode is `dotnet run --project tools/kg/kg-codegen/KgCodegen -- --root . --check`;
status ledger is the design doc's phase list, marked `✅ Built`. **None of this is portable —
it is what discovery returned for one project.**

## Part B — Reconcile state across every independent signal

Ask what is already built, and collect the answer from *every* source available, separately:

- artifacts on disk (parser/generator files that exist)
- VCS working-tree status (tracked, untracked, modified)
- codegen check-mode output (per-label node counts — the only signal that reflects real behavior)
- the phase-status ledger
- presence or absence of phase artifacts still awaiting validation

**On disagreement: stop and report the conflict. Do not pick a winner.** A source being newer, or
more convenient, is not evidence that it is right.

Common disagreement shapes, each meaning something different:

| Signal pattern | What it actually means |
|---|---|
| on disk, not in VCS | implemented but uncommitted — invisible to any fresh session |
| in VCS, artifacts still present | implemented but **not validated** |
| ledger says done, check-mode count is zero | the parser exists but silently yields nothing |
| check-mode count non-zero, ledger silent | built but unrecorded — the ledger is stale |

That third row is the dangerous one: a convention-dependent parser that stops matching does not
error, it returns zero, which reads as success. Treat a previously-nonzero label dropping to zero
as a failure, never as "nothing to do".

*Worked example (ECommerceApp):* at one point a phase was simultaneously implemented on disk,
absent from version control, still holding an un-deleted validation artifact, and unmarked in the
ledger — four sources, three different answers to "is this phase done?". A precondition check that
only asked "does the ontology file exist?" would have sailed past all of it.

## Naming and numbering

Phase numbering is **per-series with a distinguishing slug**, not globally unique. Several
unrelated series can legitimately share a number in the same directory. Never renumber a series
you are not planning.

If artifacts are written to a shared location alongside other initiatives, include a
project/tool-specific token in the filename so two unrelated "Phase 4" series stay
distinguishable.

## Dry run for this step

Read every resolved path back and confirm it exists and has the expected shape. Report the
resolved profile and the reconciled state before moving on — including a clean result.

See [verification.md](verification.md) for the full ladder.
