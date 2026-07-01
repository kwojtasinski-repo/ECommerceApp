---
name: archetype-scanner
description: Scan domain requirements against known archetypes. Launches one agent per live mapper in parallel, tolerates missing mappers as known gaps, and can also include reference-only archetypes from validated demo material. Produces fit/ directory with individual results and a combined report.
argument-hint: "[domain requirements or feature description]"
---

# Archetype Scanner

Run all known archetype mappers against a single set of domain requirements **in parallel**. Each live archetype agent independently performs its fit test and — if the archetype fits — produces a full mapping. Reference-only archetypes are recorded as known knowledge references and are not treated as implementation defects when no mapper exists. A merge agent then consolidates everything into one document.

**Output goal**: A single directory (`fit/`) containing individual archetype results (only for archetypes that fit) and a merged summary showing which archetypes matched, which didn't, and how domain concepts distribute across matched archetypes.

## When to Use

- Early domain modeling — "which archetypes apply to these requirements?"
- Requirements review — systematically check for known patterns
- Training / workshops — demonstrate archetype recognition on real requirements
- Before detailed modeling — narrow down which archetype mapper to dive deeper into

## When NOT to Use

- You already know which single archetype to apply → use that mapper directly
- Requirements are too vague for any archetype to assess (no concrete operations described)

---

## Archetype Registry

Each entry maps to either:

- a live `*-archetype-mapper` skill, or
- a validated reference document when a mapper does not yet exist.

Missing mappers are expected during early exploration. Do not fail the scan because a mapper is missing.

| ID | Mode | Asset | One-line fit question |
|----|------|-------|----------------------|
| `accounting` | live | `accounting-archetype-mapper` | "Can I ask 'how much X does S have?' and get a number with transaction history?" |
| `pricing` | live | `pricing-archetype-mapper` | "Is there a computed price/rate that depends on context, time, or components?" |
| `party` | reference | `5-znanewzorce-demo/demo-party-miro.md` | "Do I have named participants, roles, and hierarchy rather than actions or balances?" |
| `catalog` | reference | knowledge reference | "Do I model a product/content catalog with versioned descriptors and stable identifiers?" |
| `workflow` | reference | knowledge reference | "Do I model a process with states, transitions, and handoffs?" |
| `inventory` | reference | knowledge reference | "Do I track available/reserved stock or other mutable resource quantities?" |
| `document` | reference | knowledge reference | "Do I model document-like artifacts with versions, revisions, or attachments?" |
| `order` | reference | knowledge reference | "Do I model a business order with lifecycle and fulfillment steps?" |
| `shipment` | reference | knowledge reference | "Do I model delivery/transport lifecycle and status progression?" |
| `booking` | reference | knowledge reference | "Do I model reservations or time slots that can be held and released?" |
| `payment` | reference | knowledge reference | "Do I model monetary or credit movement with capture, void, refund, or authorization?" |
| `subscription` | reference | knowledge reference | "Do I model recurring entitlement, billing cycles, or renewal state?" |
| `case` | reference | knowledge reference | "Do I model a tracked issue, ticket, or case with workflow and responsibility?" |
| `asset` | reference | knowledge reference | "Do I model a managed resource with identity, ownership, and value?" |
| `audit` | reference | knowledge reference | "Do I model immutable traceability, event trails, or compliance evidence?" |
| `healthcare` | reference | knowledge reference | "Do I model regulated clinical or care workflows with governed entities?" |
| `insurance` | reference | knowledge reference | "Do I model policies, claims, coverage, and risk-related processes?" |
| `telecom` | reference | knowledge reference | "Do I model service plans, usage, provisioning, or network-related domain concepts?" |
| `retail` | reference | knowledge reference | "Do I model merchandising, catalog-to-order flow, or store/channel-specific operations?" |
| `banking` | reference | knowledge reference | "Do I model regulated financial products, balances, and transaction-heavy flows?" |
| `manufacturing` | reference | knowledge reference | "Do I model production, parts, routing, or shop-floor processes?" |
| `government` | reference | knowledge reference | "Do I model public-sector processes, approvals, or regulated case handling?" |

**Extending**: Add a new row to this table. The workflow below iterates over all rows and treats `live` and `reference` rows differently.

---

## Scanner rules

- ALWAYS scan live mappers and reference-only archetypes separately.
- DO NOT assume the registry is complete.
- DO NOT fail the scan because a mapper is missing.
- NEVER invent archetype content to fill a gap.
- ALWAYS report available matches, even when the library is incomplete.
- ALWAYS mark reference-only archetypes as knowledge references, not mapper results.

---

## Workflow

### Step 0: Get Requirements

- If provided as argument, use directly.
- If not, scan conversation for domain context. If found, summarize in 2–3 sentences and confirm.
- Only if nothing available, ask:
  > "Describe the domain — what entities exist, what operations change state, what values are tracked?"

Store the requirements text — it will be passed verbatim to every live archetype agent and used as the reference context for reference-only archetypes.

---

### Step 1: Create Output Directory

Create directory structure:

```
fit/
├── accounting.md      # (only if fit)
├── pricing.md         # (only if fit)
├── party-reference.md # (only if reference-only archetype exists)
└── summary.md         # always — merged result
```

If invoked within an orchestrator with a `task_path`, place `fit/` under `{task_path}/analysis/fit/`. Otherwise create `fit/` in the current working directory.

---

### Step 2: Launch Live Archetype Agents in Parallel

For **every live entry** in the Archetype Registry, launch one agent. All live agents launch in a **single message** for parallel execution.

Each agent:
- **Tool**: Agent tool with `subagent_type` matching the skill name from the registry
- **Prompt structure**:

```
Apply the [archetype name] archetype to the following domain requirements.

## Requirements

[paste full requirements text]

## Instructions

1. Run your fit test first.
2. If the archetype does NOT fit — return ONLY:
   ---
   archetype: [id]
   fit: false
   reason: [1-2 sentence reason]
   ---
   Do not produce a mapping. Stop here.

3. If the archetype FITS — run your full mapping workflow.
   - Ask clarifying questions via AskUserQuestion as normal.
   - Produce your complete output.
   - Prepend this header to your output:
   ---
   archetype: [id]
   fit: true
   ---

4. Write your output to: [fit_directory]/[archetype_id].md
```

Reference-only entries are not launched. They are recorded in the summary as known references.

**Wait for ALL live agents to complete before continuing.**

---

### Step 3: Collect Results

After all live agents complete:

1. Read each `fit/[id].md` file that exists for live agents
2. For agents that returned `fit: false` — note the archetype and reason
3. For agents that returned `fit: true` — note the archetype and read the full mapping
4. For reference-only archetypes — note the source document and treat as knowledge reference, not fit/no-fit
5. If reference-only archetypes exist, write `fit/[id]-reference.md` with the source doc and the one-line reason it is reference-only

Build a results table:

| Archetype | Mode | Fit? | Key Reason / Mapped Value |
|-----------|------|------|---------------------------|
| accounting | live | Yes/No | [1-line summary] |
| pricing | live | Yes/No | [1-line summary] |
| party | reference | Reference-only | [1-line summary / source doc] |
| catalog | reference | Reference-only | [1-line summary / no mapper yet] |
| workflow | reference | Reference-only | [1-line summary / no mapper yet] |
| inventory | reference | Reference-only | [1-line summary / no mapper yet] |
| document | reference | Reference-only | [1-line summary / no mapper yet] |
| order | reference | Reference-only | [1-line summary / no mapper yet] |
| shipment | reference | Reference-only | [1-line summary / no mapper yet] |
| booking | reference | Reference-only | [1-line summary / no mapper yet] |
| payment | reference | Reference-only | [1-line summary / no mapper yet] |
| subscription | reference | Reference-only | [1-line summary / no mapper yet] |
| case | reference | Reference-only | [1-line summary / no mapper yet] |
| asset | reference | Reference-only | [1-line summary / no mapper yet] |
| audit | reference | Reference-only | [1-line summary / no mapper yet] |
| healthcare | reference | Reference-only | [1-line summary / no mapper yet] |
| insurance | reference | Reference-only | [1-line summary / no mapper yet] |
| telecom | reference | Reference-only | [1-line summary / no mapper yet] |
| retail | reference | Reference-only | [1-line summary / no mapper yet] |
| banking | reference | Reference-only | [1-line summary / no mapper yet] |
| manufacturing | reference | Reference-only | [1-line summary / no mapper yet] |
| government | reference | Reference-only | [1-line summary / no mapper yet] |

---

### Step 4: Merge — Launch Summary Agent

Delegate summary generation to a merge agent. Use the Agent tool (general-purpose):

```
You are a domain modeling summary agent. Merge archetype scan results into a single report.

## Scan Results

### Archetypes That Fit
[For each live mapper: paste archetype ID + full mapping output]

### Archetypes That Did Not Fit
[For each live mapper: archetype ID + reason]

### Reference-Only Archetypes
[For each reference-only archetype: archetype ID + source doc + what it may indicate]

## Requirements (original)
[paste requirements]

## Your Task

Write `summary.md` to [fit_directory]/summary.md with this structure:

# Archetype Scan: [domain name]

## Quick View

| Archetype | Fit | Core Reason |
|-----------|-----|-------------|
| ... | ✅ / ❌ | ... |

## Matched Archetypes

For each archetype that fit:
### [Archetype Name] ✅
- **What matched**: 1-2 sentences on what domain aspect this archetype covers
- **Domain value / key entity**: the central concept identified by this archetype
- **Key decisions surfaced**: list 3-5 most important clarifying questions and answers

## Domain Concept Distribution

A single table showing where EVERY significant domain concept landed:

| Domain Concept | Archetype(s) | Role in Archetype | Unmapped? |
|----------------|-------------|-------------------|-----------|
| [concept] | accounting | Account / Transaction / ... | |
| [concept] | party | Role / Relation / ... | |
| [concept] | — | — | ⚠️ Yes |

This table must include:
- Concepts mapped by exactly one archetype
- Concepts mapped by multiple archetypes (show all — this reveals overlap worth discussing)
- Concepts NOT mapped by any archetype (these need separate modeling decisions)

## Overlaps

If any domain concept appears in multiple archetypes, describe the overlap and what it means:
- Is it the same concept seen from different angles? (normal — e.g., "Customer" in party + accounting)
- Is it a conflict that needs resolution? (rare but important)

## Gaps — Not Covered by Any Archetype

List domain concepts that no archetype claimed. For each:
- What it is
- Why no archetype covers it (state machine? workflow? integration? pure CRUD?)
- Suggested next step (e.g., "consider problem-class-classifier", "model directly", "needs custom archetype")

## Archetype Rejection Reasons

For each live archetype that did NOT fit, one line explaining why. This helps future readers understand what was considered and ruled out.

For reference-only archetypes, explain why no mapper exists yet and what evidence the reference provides.
```

---

### Step 5: Present Results

```
Archetype scan complete.

Matched: [list of ✅ live archetypes]
No fit:  [list of ❌ live archetypes]
References: [list of reference-only archetypes]

Results:
[For each matched archetype]
  - fit/[id].md — full mapping

Summary: fit/summary.md

Key findings:
- [top 2-3 insights from the summary — overlaps, gaps, surprises]
- [reference-only archetype observations]
```

---

## Error Handling

| Situation | Action |
|-----------|--------|
| Agent times out | Use results from completed agents; note incomplete scan in summary |
| All archetypes return no-fit | Summary still generated — focuses on gaps section and suggests next steps |
| Agent produces no output file | Treat as no-fit with reason "agent produced no output" |

---

## Integration

This skill can be invoked:
- Standalone: `/archetype-scanner [requirements]`
- From development-orchestrator: as an optional analysis step
- From workshops/training: to demonstrate parallel archetype recognition
