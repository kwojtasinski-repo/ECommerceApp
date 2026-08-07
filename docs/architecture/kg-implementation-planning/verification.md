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
| Step 4 | build + test + codegen check mode; observed counts against the floors predicted in Step 1 |

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
- an artifact would have to state something not traceable to a real file.

Asking costs one round trip. Guessing costs a phase built on a false marker, discovered during
validation at the earliest.

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
