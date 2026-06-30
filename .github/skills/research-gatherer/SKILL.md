---
name: research-gatherer
description: >
  Strict research orchestrator for ECommerceApp. Runs a human-gated workflow for
  planning, gathering, verification, and synthesis. Defaults to interactive mode;
  use --yolo only when the human explicitly authorizes full autonomy.
argument-hint: "<question> [--type=internal|external|mixed] [--mode=full|plan-only|sources-only|synthesize-only] [--yolo]"
---

# Research Gatherer

Use this skill when you need evidence-backed research, not a freeform answer.
The workflow is intentionally strict so a weaker model cannot silently skip steps.

## Default contract

- **Language**: English only.
- **Mode**: interactive by default.
- **Human approval**: required unless `--yolo` is explicitly present.
- **Output**: write findings to files first; do not return chat-only results.
- **Synthesis rule**: never synthesize during gathering.
- **Source rule**: never invent sources from memory.

## When to use

- You need a structured research run over code, docs, config, or external sources.
- You want planning, source selection, evidence gathering, and verification in one flow.
- You want the human to approve scope and source decisions before deeper work.

## When not to use

- You already know the answer and only need a quick lookup.
- You only need a code change plan or implementation work.
- You only need a single source file read.

## Invocation modes

### Full flow

Use the default mode for the complete workflow:

1. Classify the request as `internal`, `external`, or `mixed`.
2. Extract scope, exclusions, and success criteria.
3. Build a research plan and source manifest.
4. Pause for human approval unless `--yolo`.
5. Gather evidence from the chosen sources.
6. Verify cross-source consistency.
7. Synthesize the result.

### Plan only

Use when the human only wants source decisions and scope:

- Output the research type.
- Output scope, exclusions, and success criteria.
- Output the source strategy.
- Stop before gathering.

### Sources only

Use when the human only wants the source decision:

- Decide which source classes matter.
- List candidate files, docs, config, and/or external sources.
- Stop before evidence gathering.

### Synthesize only

Use when the findings already exist:

- Read the existing findings files.
- Produce summary, verification, and gaps.
- Do not gather new evidence unless the human explicitly approves it.

## Required checkpoints

### Checkpoint 1 — scope approval

Stop and ask the human before gathering if any of these are unclear:

- scope
- source class
- exclusions
- success criteria
- whether external research is allowed

### Checkpoint 2 — source approval

Stop and ask the human before expanding source coverage if:

- the planner found a new source family
- the request changed from internal to mixed
- the answer requires web research
- the current evidence is too thin

### Checkpoint 3 — synthesis approval

Stop and ask the human before final synthesis if:

- there are contradictions
- the evidence quality is low
- the scope widened during gathering
- the user did not choose `--yolo`

## Workflow

### 1. Preflight

- Read `.github/context/agent-decisions.md`.
- Read `.github/instructions/agent-workflow.instructions.md`.
- Read `.github/instructions/mcp-routing.instructions.md`.
- If the request touches repo state, also check `.github/context/project-state.md` and `.github/context/known-issues.md`.

### 2. Classify

Determine one of:

- `internal` = code, docs, config, tests, ADRs, context files
- `external` = official docs, standards, public web sources
- `mixed` = both internal and external evidence are required

If the classification is ambiguous, ask the human. Do not guess.

### 3. Plan

Write or draft:

- `planning/research-brief.md`
- `planning/research-plan.md`
- `planning/sources.md`

The plan must include:

- research question
- scope in/out
- success criteria
- source categories
- gathering strategy
- verification approach

### 4. Gather

Collect evidence into `analysis/findings/`.

Rules:

- one file per source category or source family
- every claim needs a citation
- code claims need file paths and line numbers
- web claims need URLs and quotes
- keep rejected-but-relevant information in a dedicated section

### 5. Verify

Cross-check the gathered evidence.

- code vs docs
- config vs code
- tests vs behavior
- internal vs external when mixed

### 6. Synthesize

Produce:

- `analysis/findings/00-summary.md`
- `analysis/findings/99-verification.md`
- `analysis/findings/98-rejected.md` if relevant

## Hard rules

- MUST stop for human approval unless `--yolo`.
- MUST never skip the planner step.
- MUST never synthesize inside the gather step.
- MUST never pretend a source was checked if it was not.
- MUST never omit verification.
- MUST never omit rejected information when something relevant was excluded.
- MUST never answer from memory when evidence is available.
- NEVER merge findings without checking contradictions.

## Suggested file layout

```text
planning/
  research-brief.md
  research-plan.md
  sources.md
analysis/
  findings/
    00-summary.md
    98-rejected.md
    99-verification.md
    codebase-*.md
    docs-*.md
    config-*.md
    external-*.md
```

## Stop format

When not in `--yolo`, always stop with a clear approval prompt.

Example:

```text
RESEARCH GATHERER: scope ready
Awaiting human approval: APPROVE / REVISE / ABORT
```
