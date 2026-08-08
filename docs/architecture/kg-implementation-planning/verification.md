# Verification — dry-run ladder, stop triggers, host-aware asking

Verification is not a step in the flow. It runs **after every step**, and it means something
different each time.

← back to [README.md](README.md)

## The dry-run ladder

Each rung checks that what the step just produced actually holds together. **Report the outcome
either way** — a clean pass is real information, especially in an interactive session. Do not
silently proceed on success.

| After | What "dry run" means here |
|---|---|
| Step 0 | read every resolved path back; confirm it exists and has the expected shape |
| Step 1 | confront the derived phase order against observed built state; a contradiction stops the session |
| Step 2 | **the real one** — hand-trace 2–3 actual code instances through the proposed marker and heuristic |
| Step 3 | re-read the emitted artifact; every claim must trace to a Step 2 finding with `file:line` |
| Step 4 | build + test + codegen check mode; observed counts against the floors predicted in Step 1; and for each defect class the phase could plausibly ship, name the test layer that would catch it (see below) |

The Step 2 rung is the one that finds real problems, and the one most often skipped under time
pressure. It is not optional and not delegable to the implementing agent.

## Stop-and-ask triggers

"Verify continuously, never fully trust" dissolves into nothing unless the triggers are
enumerated. Stop and ask when:

- the ontology declares a label for which discovery found **no** marker;
- discovery found **two or more plausible** markers;
- the derived phase order contradicts the observed built state;
- state signals disagree about whether a phase is done (see
  [project-profile.md](project-profile.md));
- documentation asserts something the code does not confirm;
- a heuristic's coverage falls below what the plan predicted;
- a previously-nonzero label yields zero — silence is not success;
- a phase introduces a **new warning kind** whose population is an expected non-match — noise is
  not success either (see below);
- a defect class the phase can plausibly ship has **no test layer able to observe it** (see "Three
  test layers" below) — adding a fourth assertion to the layer that is already blind to it is not a
  fix;
- a count would be published with no way to regenerate it, or pinned in a test as a total rather
  than as a fact;
- an artifact would have to state something not traceable to a real file.

Asking costs one round trip. Guessing costs a phase built on a false marker, discovered during
validation at the earliest.

## Warning discipline is half of the zero-yield guardrail

"A parser that stops matching returns zero instead of erroring" is the failure this whole ladder
exists to catch. That signal only works if it arrives somewhere a human reads. So the guardrail has
two halves, and a phase satisfies neither by satisfying only one:

- **Warn for every genuine unresolved reference.** A resolution that fails silently is a graph with
  a hole in it and no record of where.
- **Stay silent for every expected non-match.** Files the convention deliberately does not cover,
  empty collections, references that resolve to a modelled-elsewhere concept — none of these are
  failures, and reporting them as failures trains everyone to skim past the warning block.

Phase 5 of this repo's build is the worked example. `ScriptModuleParser` reported two Razor views
as unresolved on every run because it resolved the view→`Page` link *before* knowing whether any
edge depended on it; both views were rendered under a different action name and contained only
same-host calls that the phase explicitly required to be silent. Nothing was wrong with the graph —
but a permanent two-line false population sat directly in front of the drop-to-zero signal.

**Verification rule:** a phase that introduces a new warning *kind* must account for its whole
population, entry by entry, and show that every entry is a genuine unresolved reference. Pin the
real-tree warning count in a test, the same way node and edge counts are pinned. "Warnings are
expected in this tool" is not an account.

## Three test layers, and what only each one can catch

A phase is not verified by "the suite is green". It is verified by knowing **which layer could
have observed this being wrong**, and confirming that layer exists. Green suites have shipped six
defects in this repo's own build, every one because the question was never asked.

| Layer | Runs against | What only it can catch | What it is structurally blind to |
|---|---|---|---|
| **Source-level / contract** | no data — reads the code as text | Rules about the *shape of the code* that must outlive today's implementations: no query language outside the layer that owns it, no write path in a read-only component, no new entry point bypassing a mandatory guard | Anything about behaviour. It confirms code *looks* right |
| **Fixture / behavioural** | a hand-built, ephemeral input | Exact named behaviour, including the negative cases: this input resolves to that output, this row grades `ambiguous` and that one `high` | Any shape nobody thought to put in the fixture — which is exactly the shape a bug hides in |
| **Real-input end-to-end** | the actual repository, regenerated per run | Whatever the codebase really contains, including inputs no one would invent: the real ambiguous name, the real node reachable two ways, the real id worth typoing | Precision. It tells you the pipeline holds, not which component broke |

The middle layer is where most teams stop, and it is where this repo's most expensive defects
survived. Two worked examples, both past a green behavioural suite: a traversal reported a node
once per path length instead of once per node — invisible because **both depth tests traversed the
one fixture chain with no branching**; and nine of ten tools returned an empty list for an id
matching nothing — invisible because **no test ever passed an id that did not exist**.

So the rules the layers imply:

- **A behavioural test is only as good as the topology its fixture contains.** When adding a
  traversal or a parser, add the *shape it can be wrong about* to the fixture. The cheapest way to
  find out whether you did is to break the code and watch the test go red.
- **Assert the error paths, not only the happy one.** For anything query-shaped, "input does not
  exist" and "input of the wrong kind" are mandatory assertions. An empty result for either makes a
  typo indistinguishable from a true negative — silent by construction, and the root cause of five
  separate defects here.
- **A real-input layer is not optional once a phase produces a serving surface.** It is the only
  layer that can fail on what the repository actually contains. Budget for it: this repo's takes
  ~56 s and needs a container, and that is the honest price.
- **The real-input layer must hardcode no expected value.** Read the expected counts back out of
  what the generator printed on that run and compare with what the store received; cross-check any
  derived quantity against an independent oracle that shares no code with the thing under test. A
  test restating the implementation's own query proves only that the query is deterministic.

## A published number carries its reproduction command

Counts describe a moving tree. The moment one is written into a document, it starts drifting from
the code, and the reader a year later has no way to tell whether "32" is a fact, a stale fact, or a
typo. Two failures follow, and both have happened here:

- **A number read as a defect.** A generated total and a loaded total were compared as if they were
  one population; they were two (instance edges versus schema edges), and the "discrepancy" cost a
  round of investigation before it was recognized as arithmetic.
- **A number nobody can confirm or retire.** Once its derivation is lost, it can only be copied
  forward, and every later document inherits it.

**Rule:** every count a phase publishes — in an ADR, a README, a reference doc — ships with a
one-line command that regenerates it from scratch, and states what population it counts when that
is not self-evident. Prefer a table gathering them in one place over numbers scattered through
prose. Say explicitly that the numbers are measurements rather than decisions, so a reader knows to
re-derive rather than trust.

The mirror rule for tests: **publish numbers, pin facts.** A test asserting "there are 1330 edges"
breaks on every legitimate commit and gets weakened to `>= 1` or deleted. Pin the things that are
*true regardless of size* — that this entity maps to that table, that this decorator produces that
edge, that the count the generator reported equals the count the database received — and let the
totals live in documentation where a reproduction command keeps them honest.

## Host-aware asking

Carried here rather than referenced from a project instruction file, because the skill must be
self-contained. Four tiers, most capable first:

- **Claude Code** — a closed-set question tool (`AskUserQuestion`) for bounded choices; plain free
  text for open-ended input.
- **VS Code / Copilot in VS Code** — `vscode_askQuestions`, freeform input, options only when the
  choice is a genuinely closed set.
- **Any other interactive host** (CLI agents, other IDEs) — a plain chat question with numbered
  options, then **stop and wait**. No API equivalent is needed; this is the
  lowest-common-denominator fallback and it works everywhere.
- **Non-interactive host** — fail loudly, listing exactly what is missing. Never guess.

### Which form for which question

| Question | Form |
|---|---|
| output mode | closed set |
| phase count and boundaries | closed set |
| which ontology file is authoritative | closed set, from discovered candidates |
| transport/deployment choices for a serving phase | closed set, **no default** |
| anything open-ended about intent or scope | free text |

Do not force an open-ended question into a closed set to make it easier to answer. That is how a
design decision gets made by the option list instead of by the human.
