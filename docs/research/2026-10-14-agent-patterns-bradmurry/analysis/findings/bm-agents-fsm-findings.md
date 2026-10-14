# Findings: bm-agents-fsm

Category: bm-agents-fsm (bradmurry posts on agents, FSM and reactive reducer)
Gathered: 2026-10-14 via WebFetch (6 of 6 URLs returned content; none failed outright)
Plan: `docs/research/2026-10-14-agent-patterns-bradmurry/planning/research-plan.md`

## Method and Confidence Rule

- Every quote below was returned by WebFetch. WebFetch passes page text through a summarizing model, and five of the six responses stated that the full article could not be reproduced; they returned short excerpts (each under about 125 characters). Quotes are therefore "as returned by the fetch tool", not checked against raw HTML. See Gaps G-1 and G-2.
- Confidence rule applied (plan: single-source claims are at most Medium until verification):
  - Medium: explicit, self-contained verbatim-style quote that states the pattern or claim.
  - Low: quote is a fragment or lead-in, the pattern is described only in fetch-summary text with no quote, or the quote's referent is ambiguous.
  - High is not assigned in this phase. All six pages are one author, so no finding is corroborated independently.
- Layer tags: Big Picture (problem classes, why agents fail), Modeling (FSM, reducer, states, contracts), Implementation (concrete mechanics, runtime, ops, UI).

## Source Key

| Key | URL |
|---|---|
| S1 | https://bradmurry.com/software/agents-in-production-intro/ |
| S2 | https://bradmurry.com/software/self-evolving-agents-intro/ |
| S3 | https://bradmurry.com/software/reactive-reducer-patterns/ |
| S4 | https://bradmurry.com/software/reactive-reducer-patterns-intro/ |
| S5 | https://bradmurry.com/software/walking-a-reentrant-human-in-the-loop-fsm-step-by-step/ |
| S6 | https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ |

Each finding's Source line gives the key and the full URL.

---

## Source S1: Agents in Production (intro)

## F-01: Agent in production is a distributed system
- Layer: Big Picture
- Source: S1, https://bradmurry.com/software/agents-in-production-intro/ — "An agent in production is a distributed system."
- Evidence: Stated under "What the series argues", Claim 1.
- Confidence: Medium — explicit claim, single source, excerpt-level fetch.

## F-02: Distributed-systems concerns that agents must handle
- Layer: Big Picture
- Source: S1, https://bradmurry.com/software/agents-in-production-intro/ — "State, events, failure handling, replay, backpressure, idempotency, observability."
- Evidence: Listed as the concepts the series borrows from distributed systems (opening section).
- Confidence: Medium — explicit list; single source.

## F-03: Failures as the starting point of each article
- Layer: Big Picture
- Source: S1, https://bradmurry.com/software/agents-in-production-intro/ — "Each article starts from a real production failure, connects it to a solved systems problem,"
- Evidence: Series structure statement ("Article structure").
- Confidence: Medium — explicit; the quote is cut mid-sentence by the fetch excerpt.

## F-04: Cost budget treated as a correctness-level concern
- Layer: Big Picture
- Source: S1, https://bradmurry.com/software/agents-in-production-intro/ — "a cost budget that gets watched as closely as correctness."
- Evidence: Opening section; the operational list (evals, rollouts, drift monitoring, cost budgets) comes from the fetch summary, not a quote, so only the quoted sentence is recorded as a finding.
- Confidence: Medium — explicit quote; single source.

## F-05: Deterministic shell around a probabilistic model
- Layer: Modeling
- Source: S1, https://bradmurry.com/software/agents-in-production-intro/ — "by putting the nondeterministic part inside a deterministic shell." and "A probabilistic model only works inside a deterministic shell."
- Evidence: Opening section and Claim 2 ("Control flow outside the prompt").
- Confidence: Medium — two explicit quotes stating the same claim; single source.

## F-06: Control flow lives in named states, guards, and declared transitions
- Layer: Modeling
- Source: S1, https://bradmurry.com/software/agents-in-production-intro/ — "control flow lives in named states, guards, and declared transitions with a durable position that survives a crash."
- Evidence: Claim 2.
- Confidence: Medium — explicit; single source.

## F-07: Model output is a value; the machine assigns its meaning
- Layer: Modeling
- Source: S1, https://bradmurry.com/software/agents-in-production-intro/ — "The model contributes a value, and the machine decides what that value means for the next step."
- Evidence: Claim 2.
- Confidence: Medium — explicit; single source.

## F-08: Capacity inputs from day one
- Layer: Implementation
- Source: S1, https://bradmurry.com/software/agents-in-production-intro/ — "concurrency limits, queue depth, and cost per workflow are capacity inputs from day one."
- Evidence: Claim 3 ("Operating the shell").
- Confidence: Medium — explicit; single source.

## F-09: Workflow definition as a versioned, canaried artifact
- Layer: Implementation
- Source: S1, https://bradmurry.com/software/agents-in-production-intro/ — "The workflow definition is a versioned artifact that gets rolled out, canaried, and rolled back like any other change,"
- Evidence: Claim 3. The quote is cut at a comma by the fetch excerpt.
- Confidence: Medium — explicit; single source.

## F-10: Series outline of production patterns (Act II and Act III)
- Layer: Big Picture
- Source: S1, https://bradmurry.com/software/agents-in-production-intro/ — "Idempotent effects, event journaling, replay and recovery, backpressure, human review" (Act II); "Memory architecture, decision observability, multi-agent coordination, and the enterprise control plane." (Act III)
- Evidence: "How it is structured" section. This is a series map. Only the outline is recorded; the linked part pages were not fetched (see Rejected Information).
- Confidence: Medium — explicit list; single source; sub-articles unverified.

---

## Source S2: Self-Evolving Agents (intro)

## F-11: Self-evolving harness as "point the agent at a general problem"
- Layer: Big Picture
- Source: S2, https://bradmurry.com/software/self-evolving-agents-intro/ — "A self-evolving agent harness lets you point an agent at a general problem"
- Evidence: "The premise" section. Excerpt ends mid-sentence in the fetch result.
- Confidence: Medium — explicit; single source.

## F-12: Human supplies the task and the yardstick
- Layer: Big Picture
- Source: S2, https://bradmurry.com/software/self-evolving-agents-intro/ — "The human gives the task and the yardstick." (also "give the task, give the yardstick", in "Until next time")
- Evidence: Premise section; repeated in the closing section.
- Confidence: Medium — explicit; repeated within the same page.

## F-13: Baseline without a quality gate proves nothing
- Layer: Big Picture
- Source: S2, https://bradmurry.com/software/self-evolving-agents-intro/ — "A clean journal is not a good outcome. It's the absence of a check."
- Evidence: Section "Round zero".
- Confidence: Medium — explicit; single source.

## F-14: Discovery loop: RUN, MEASURE, DISCOVER, BUILD (phase names unverified)
- Layer: Implementation
- Source: S2, https://bradmurry.com/software/self-evolving-agents-intro/ — "The agent discovers the process itself, one measurement at a time."
- Evidence: The quoted sentence is explicit. The four phase names (RUN, MEASURE, DISCOVER, BUILD) appear only in the fetch summary, not in any returned quote.
- Confidence: Low — phase names not confirmed against page text; quote supports the discovery idea only.

## F-15: Stop at convergence on the agent's own measurements
- Layer: Implementation
- Source: S2, https://bradmurry.com/software/self-evolving-agents-intro/ — "The agent keeps iterating until its own measurements say the work is done."
- Evidence: Section "The discovery loop".
- Confidence: Medium — explicit; single source.

## F-16: Facts before opinion; LLM critic as weak evidence
- Layer: Implementation
- Source: S2, https://bradmurry.com/software/self-evolving-agents-intro/ — "The rule is iron: facts before opinion." and "Treat it as a hint." (the referent "it" is the LLM critic per the fetch summary)
- Evidence: Section "Three signals for discovering what's actually broken".
- Confidence: Medium — first quote explicit; "Treat it as a hint" is a fragment whose referent comes from the summary, so the critic reading is not confirmed from the quote alone.

## F-17: Deterministic core handled by a tool; variable layer handled by the LLM
- Layer: Modeling
- Source: S2, https://bradmurry.com/software/self-evolving-agents-intro/ — "The deterministic core." and "the LLM's generalization is the feature, not the bug."
- Evidence: Section "Every task has two halves".
- Confidence: Low — first quote is a two-word fragment; the split is partly inferred from the summary.

## F-18: A workflow with no gate cannot fail
- Layer: Big Picture
- Source: S2, https://bradmurry.com/software/self-evolving-agents-intro/ — "A workflow with no quality gate cannot fail, so a clean journal proves nothing about the output."
- Evidence: Section "A clean journal is not a good outcome".
- Confidence: Medium — explicit; single source.

## F-19: Iteration disciplines
- Layer: Implementation
- Source: S2, https://bradmurry.com/software/self-evolving-agents-intro/ — "One change at a time." / "Check before you build." / "Report the regression." / "Report the ceiling." / "It does not loop hoping something will change."
- Evidence: Section "The disciplines: how the agent avoids false turns". Each quote is a separate short rule.
- Confidence: Medium — explicit rules; single source.

## F-20: Versioned workflow accumulating discovered fixes
- Layer: Implementation
- Source: S2, https://bradmurry.com/software/self-evolving-agents-intro/ — "The agent discovered them, one measurement at a time." (preceded by "No human designed this workflow or noticed these problems.")
- Evidence: Section "This is real: five archived versions of one workflow". The count "five" is from the section heading as returned.
- Confidence: Medium — explicit; single source; the example is a self-report by the author.

---

## Source S3: Reactive Reducer Patterns (series index, 13 patterns)

## F-21: The machine moves itself; actors only contribute values
- Layer: Modeling
- Source: S3, https://bradmurry.com/software/reactive-reducer-patterns/ — "The machine moves itself. Actors only ever contribute values."
- Evidence: Section "Intro: Why the reactive reducer?".
- Confidence: Medium — explicit; single source. (The same idea recurs in S4 and S6.)

## F-22: Pattern 1, Sequential Pipeline: named context fields for traceability
- Layer: Modeling
- Source: S3, https://bradmurry.com/software/reactive-reducer-patterns/ — "every stage's output is a _named_ context field, so a bad value is traceable to the exact stage that wrote it."
- Evidence: Section "1. Sequential Pipeline (Failure points)". Link target listed in S3 and S4 (not fetched).
- Confidence: Medium — explicit; single source.

## F-23: Pattern 2, Parallel Fan-Out / Fan-In: trade-off stated
- Layer: Modeling
- Source: S3, https://bradmurry.com/software/reactive-reducer-patterns/ — "The pattern trades coordination complexity for lower latency and broader coverage."
- Evidence: Section "2. Parallel Fan-Out / Fan-In (Overview)".
- Confidence: Medium — explicit; single source.

## F-24: Pattern 3, Supervisor / Router: separate record from loop signal
- Layer: Modeling
- Source: S3, https://bradmurry.com/software/reactive-reducer-patterns/ — "Accumulate what you want to keep; consume what re-arms the loop."
- Evidence: Section "3. Supervisor / Router (Failure points)". Near-identical wording is the series idiom (see F-41).
- Confidence: Medium — explicit; single source.

## F-25: Pattern 4, Hierarchical Manager / Worker: context crosses levels at defined points
- Layer: Modeling
- Source: S3, https://bradmurry.com/software/reactive-reducer-patterns/ — "Distortion is bounded to two explicit boundaries: brief in, context out."
- Evidence: Section "4. Hierarchical Manager / Worker (Failure points)".
- Confidence: Medium — explicit; single source.

## F-26: Pattern 5, Peer Handoff / Swarm: handoff loops bounded by declared paths and caps
- Layer: Modeling
- Source: S3, https://bradmurry.com/software/reactive-reducer-patterns/ — "'Bounce forever' isn't a legal run."
- Evidence: Section "5. Peer Handoff / Swarm (Failure points)". The fetch summary adds "bounded by declared escape paths and caps"; that part has no quote.
- Confidence: Medium — quote explicit; the "caps" part is summary-only.

## F-27: Pattern 6, Debate / Consensus: debate length capped by a typed verdict
- Layer: Modeling
- Source: S3, https://bradmurry.com/software/reactive-reducer-patterns/ — "Structurally bounded here: the judge's `verdict` is an enum"
- Evidence: Section "6. Debate / Consensus (Failure points)". Relevant to the brief's debate-mechanics requirement; recorded only as a pattern statement here.
- Confidence: Medium — explicit; single source; the quote is cut before the end of the sentence.

## F-28: Pattern 7, Blackboard / Shared-State: typed data model, not a scratchpad
- Layer: Modeling
- Source: S3, https://bradmurry.com/software/reactive-reducer-patterns/ — "The board is a _data model_, not a scratchpad - one level deep, deliberately."
- Evidence: Section "7. Blackboard / Shared-State (Failure points)".
- Confidence: Medium — explicit; single source.

## F-29: Pattern 8, Planner / Executor: completion is the executor's typed claim
- Layer: Modeling
- Source: S3, https://bradmurry.com/software/reactive-reducer-patterns/ — "Completion is the executor's typed claim."
- Evidence: Section "8. Planner / Executor (Failure points)".
- Confidence: Medium — explicit; single source.

## F-30: Pattern 9, Generator / Critic / Refiner: loops compare history instead of resetting state
- Layer: Modeling
- Source: S3, https://bradmurry.com/software/reactive-reducer-patterns/ — "when you stop destroying state to re-arm a loop, every test becomes a comparison"
- Evidence: Diagram note under Generator / Critic / Refiner.
- Confidence: Medium — explicit; single source; sits in a diagram caption as returned.

## F-31: Pattern 10, Auction / Contract-Net: typed contract makes every bid answer the same way
- Layer: Modeling
- Source: S3, https://bradmurry.com/software/reactive-reducer-patterns/ — "The contract-net idea made literal: the contract is the field's type"
- Evidence: Section "10. Auction / Contract-Net (Failure points)".
- Confidence: Medium — explicit; single source.

## F-32: Pattern 11, Event-Driven / Pub-Sub: external transport not yet built
- Layer: Implementation
- Source: S3, https://bradmurry.com/software/reactive-reducer-patterns/ — "Externally it isn't one yet." (the reducer handles events internally; external event transport is not built)
- Evidence: Section "11. Event-Driven / Publish-Subscribe (caveat)". This is the author's own limitation statement.
- Confidence: Medium — explicit caveat; single source.

## F-33: Pattern 12, Quorum / Voting: correlated agents give correlated errors
- Layer: Big Picture
- Source: S3, https://bradmurry.com/software/reactive-reducer-patterns/ — "Correlated agents produce correlated errors."
- Evidence: Section "12. Quorum / Voting (Failure points)". Voters sharing a model or blind spot can agree on the same mistake, per the summary.
- Confidence: Medium — explicit; single source.

## F-34: Pattern 13, Dynamic Team Formation: shared context lives in the store
- Layer: Modeling
- Source: S3, https://bradmurry.com/software/reactive-reducer-patterns/ — "The context doesn't live in the specialists; it lives in the store."
- Evidence: Section "13. Dynamic Team Formation (Failure points)".
- Confidence: Medium — explicit; single source.

---

## Source S4: Reactive Reducer Patterns (intro)

## F-35: Root cause: agents doing the control flow
- Layer: Big Picture
- Source: S4, https://bradmurry.com/software/reactive-reducer-patterns-intro/ — "the agents are doing the control flow."
- Evidence: Opening section.
- Confidence: Medium — explicit; single source.

## F-36: Non-deterministic control flow cannot be prompted into determinism
- Layer: Big Picture
- Source: S4, https://bradmurry.com/software/reactive-reducer-patterns-intro/ — "You can prompt it to be careful. You can't make it deterministic."
- Evidence: Opening section.
- Confidence: Medium — explicit; single source.

## F-37: Rule 1, context is the single source of truth
- Layer: Modeling
- Source: S4, https://bradmurry.com/software/reactive-reducer-patterns-intro/ — "context is the single source of truth."
- Evidence: "The reactive reducer" section. Same wording recurs in S6 (see F-47).
- Confidence: Medium — explicit; single source.

## F-38: Rule 2, agents advance the machine by writing context, not by requesting transitions
- Layer: Modeling
- Source: S4, https://bradmurry.com/software/reactive-reducer-patterns-intro/ — "Agents advance the machine by writing context - never by requesting a transition."
- Evidence: "The reactive reducer" section.
- Confidence: Medium — explicit; single source.

## F-39: Rule 3, microstep loop fires the first eligible, guard-passing edge
- Layer: Implementation
- Source: S4, https://bradmurry.com/software/reactive-reducer-patterns-intro/ — "A microstep loop fires the first eligible, guard-passing edge until the machine parks."
- Evidence: "The reactive reducer" section.
- Confidence: Medium — explicit; single source.

## F-40: Determinism: same (state, context) gives same edge
- Layer: Modeling
- Source: S4, https://bradmurry.com/software/reactive-reducer-patterns-intro/ — "Re-run the same (state, context) and you get the same edge."
- Evidence: "The reactive reducer" section.
- Confidence: Medium — explicit; single source.

## F-41: Token cost bounded by design, and the series idiom
- Layer: Big Picture
- Source: S4, https://bradmurry.com/software/reactive-reducer-patterns-intro/ — "The token bill is bounded by the design, not by how well the models behaved."
- Evidence: "The reactive reducer" section. This finding is about token cost only; the series idiom is recorded separately under F-24 (S3, Idioms section).
- Confidence: Medium — explicit; single source.

## F-42: One FSM, two scales; coordination separate from execution substrate
- Layer: Implementation
- Source: S4, https://bradmurry.com/software/reactive-reducer-patterns-intro/ — "The same FSM runs at two very different scales, and that's not a footnote" and "the coordination logic is separate from the execution substrate"
- Evidence: "Two ways to run it" section.
- Confidence: Medium — explicit; single source. The "two scales" are not named in the returned excerpt.

## F-43: Series scope: thirteen patterns, one reducer
- Layer: Big Picture
- Source: S4, https://bradmurry.com/software/reactive-reducer-patterns-intro/ — "Thirteen patterns. One reducer."
- Evidence: "What this series covers" section. Count matches the 13 pattern entries enumerated in S3 (F-22 to F-34), but both come from the same author and fetch method.
- Confidence: Medium — explicit; single source for the count.

---

## Source S5: Walking a Reentrant Human-in-the-Loop FSM

## F-44: Accumulating list context removes the need to clear fields
- Layer: Modeling
- Source: S5, https://bradmurry.com/software/walking-a-reentrant-human-in-the-loop-fsm-step-by-step/ — "With `list` context there is nothing to clear. Each pass appends."
- Evidence: Section "Why the loop keeps its history". Lists keep every draft, verdict, and note.
- Confidence: Medium — explicit; single source.

## F-45: The data is the loop counter
- Layer: Modeling
- Source: S5, https://bradmurry.com/software/walking-a-reentrant-human-in-the-loop-fsm-step-by-step/ — "The counter is the data"
- Evidence: Section "Why the loop keeps its history". Revision budget is derived from list length, so no separate counter field.
- Confidence: Medium — explicit heading-level quote; the quote is a short fragment.

## F-46: Comparisons replace presence checks in loop guards
- Layer: Modeling
- Source: S5, https://bradmurry.com/software/walking-a-reentrant-human-in-the-loop-fsm-step-by-step/ — "Presence stops being a signal."
- Evidence: Section "Why the loop keeps its history". Per the summary, once a field is set, `has()` stays true, so guards compare list sizes.
- Confidence: Medium — explicit; single source.

## F-47: Human-in-the-loop as a parked state
- Layer: Modeling
- Source: S5, https://bradmurry.com/software/walking-a-reentrant-human-in-the-loop-fsm-step-by-step/ — "`review` is the only state with a human in it."
- Evidence: Section "The machine". Only the review state raises a modal and waits for a person.
- Confidence: Medium — explicit; single source.

## F-48: Derived value computed with no LLM turn
- Layer: Implementation
- Source: S5, https://bradmurry.com/software/walking-a-reentrant-human-in-the-loop-fsm-step-by-step/ — "Derive first, then use:"
- Evidence: Section "Step 2, a state with nobody in it". The quote is a code-block lead-in; the mechanism (an `assign-reviewer` state computing the reviewer by expression) comes from the fetch summary.
- Confidence: Low — the quote is a lead-in only; mechanism is summary text.

## F-49: Human target resolved at state entry, not authored
- Layer: Modeling
- Source: S5, https://bradmurry.com/software/walking-a-reentrant-human-in-the-loop-fsm-step-by-step/ — "The target is resolved, not authored."
- Evidence: Section "Step 3, the machine parks and elicits a human". The reviewer is chosen from live context on entry.
- Confidence: Medium — explicit; single source.

## F-50: One modal for several fields from the same human
- Layer: Implementation
- Source: S5, https://bradmurry.com/software/walking-a-reentrant-human-in-the-loop-fsm-step-by-step/ — "Two requirements from the same human become one modal."
- Evidence: Section "Step 3". Form is derived from the context schema, per the summary.
- Confidence: Medium — explicit; single source.

## F-51: A human answer is a context write
- Layer: Modeling
- Source: S5, https://bradmurry.com/software/walking-a-reentrant-human-in-the-loop-fsm-step-by-step/ — "a modal is just a UI over a context write."
- Evidence: Section "Step 4, the human's answer is a context write". Guards then evaluate the answer.
- Confidence: Medium — explicit; single source.

## F-52: Reentrant revision cycle resets only the routing field
- Layer: Modeling
- Source: S5, https://bradmurry.com/software/walking-a-reentrant-human-in-the-loop-fsm-step-by-step/ — no verbatim quote retrieved.
- Evidence: Section "Path B, changes: the reentrant cycle" (fetch summary). The `changes` edge loops back to `draft`; the agent revises against the full feedback history; only the routing field `reviewer` is reset each round.
- Confidence: Low — described only in fetch-summary text; no verbatim quote.

## F-53: Convergence across turns is guarded by data plus iteration policy
- Layer: Modeling
- Source: S5, https://bradmurry.com/software/walking-a-reentrant-human-in-the-loop-fsm-step-by-step/ — "Cross-turn convergence is the guard on the data plus `policy.maxIterations`, and nothing else."
- Evidence: Section "Path B, changes". Per the summary, the in-step oscillation guard does not catch cycles that span multiple turns.
- Confidence: Medium — explicit; single source.

## F-54: Non-terminal abort gives the agent one turn to acknowledge
- Layer: Modeling
- Source: S5, https://bradmurry.com/software/walking-a-reentrant-human-in-the-loop-fsm-step-by-step/ — "Notice `abort` is **not** terminal."
- Evidence: Section "Path C, rejected: a clean abort".
- Confidence: Medium — explicit; single source.

## F-55: The reactive reducer pattern drives the whole workflow from context writes
- Layer: Modeling
- Source: S5, https://bradmurry.com/software/walking-a-reentrant-human-in-the-loop-fsm-step-by-step/ — "That's the reactive reducer pattern doing its job"
- Evidence: Section "The whole thing, one idea". Workflow advances only by context writes from agents, humans, or expressions.
- Confidence: Medium — explicit; single source.

---

## Source S6: Binding LLMs to Finite State Machines (reactive reducer)

## F-56: An LLM is a poor state machine
- Layer: Big Picture
- Source: S6, https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ — "An LLM is a fantastic contributor. It is a terrible state machine."
- Evidence: "A little background" section. The article argues this is what breaks production workflows.
- Confidence: Medium — explicit; single source.

## F-57: Context is the single source of truth; actors never request transitions
- Layer: Modeling
- Source: S6, https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ — "Context is the single source of truth."
- Evidence: "The core idea: nobody asks the machine to move". No `requestTransition` call; an agent's choice is a context write that a guard routes on.
- Confidence: Medium — explicit; single source. Same wording as F-37 (S4).

## F-58: Structural legality: an edge not in the YAML cannot happen
- Layer: Modeling
- Source: S6, https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ — "If an edge isn't in the YAML, it can't happen."
- Evidence: "The core idea" section; the summary states there is no fire-time legality check.
- Confidence: Medium — explicit; single source.

## F-59: Parking is a first-class state
- Layer: Modeling
- Source: S6, https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ — "Parking is a first-class state."
- Evidence: "The core idea" section. A state waiting on a human or delegate agent reports what it needs and resumes when a contribution arrives.
- Confidence: Medium — explicit; single source.

## F-60: Microstep loop with cap and no-progress guard (quote-less)
- Layer: Implementation
- Source: S6, https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ — no verbatim quote retrieved.
- Evidence: "The machine: a microstep loop" section (fetch summary). `FsmReducer.advance` runs until it parks, reaches a terminal state, hits the microstep cap (128), or detects no progress. A badly written loop becomes a config error.
- Confidence: Low — summary text only; numeric cap (128) is not confirmed against a verbatim quote.

## F-61: Ownership is derived from data; no owner or actor field
- Layer: Modeling
- Source: S6, https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ — "The machine infers who it's waiting for from the data."
- Evidence: "The DSL: a workflow as data" section. The `from` selector names the producer of each field (agent, human, peer agent, tool, or system).
- Confidence: Medium — explicit; single source.

## F-62: Guards are pure; effects live only in actions
- Layer: Modeling
- Source: S6, https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ — "A guard may never cause an effect."
- Evidence: "The DSL" section. Guards are CEL predicates over context, with no I/O or wall-clock access; effects are in entry, exit, and transition actions.
- Confidence: Medium — explicit; single source.

## F-63: Delegated review parks and summons a reviewer over A2A
- Layer: Implementation
- Source: S6, https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ — "the runtime summons the reviewer over A2A."
- Evidence: "A worked example: delegated review" section. Per the summary, the machine waits without polling.
- Confidence: Medium — explicit; single source.

## F-64: Authoring pitfall: a missing reentrant state stalls the run
- Layer: Implementation
- Source: S6, https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ — "Omit a reentrant state and no agent is authorized there - a permanent stall."
- Evidence: "A worked example" section.
- Confidence: Medium — explicit; single source.

## F-65: Rework loops cleared by an action to stop re-firing
- Layer: Modeling
- Source: S6, https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ — no verbatim quote retrieved.
- Evidence: "A worked example" section (fetch summary): clearing the trigger field in an action prevents the same edge from re-firing forever.
- Confidence: Low — summary text only.

## F-66: Joins expressed as guards using has(...)
- Layer: Modeling
- Source: S6, https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ — no verbatim quote retrieved.
- Evidence: "Guards: routing is data" section (fetch summary). Joins are guards, not special primitives.
- Confidence: Low — summary text only.

## F-67: Guard that throws counts as not passing
- Layer: Implementation
- Source: S6, https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ — "never a crash, never a half-applied transition."
- Evidence: "Guards: routing is data" section. The summary states a guard that throws counts as not passing.
- Confidence: Medium — quote explicit; the attached mechanism is summary text.

## F-68: The agent reads the store rather than holding the workflow in its head
- Layer: Implementation
- Source: S6, https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ — "the agent doesn't hold the workflow in its head."
- Evidence: "What the agent actually sees" section. Each turn the runtime injects current context. State descriptions can embed values with `{{ context.<field> }}` (summary).
- Confidence: Medium — explicit quote; templating syntax is summary text.

## F-69: Claim-checking: large fields stored externally, referenced in the envelope
- Layer: Implementation
- Source: S6, https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ — "Full drafts and files never bloat the envelope."
- Evidence: "Durability" section. Portable state sits in a copy-on-write envelope with version checks (summary text).
- Confidence: Medium — explicit quote; envelope/version-check detail is summary text.

## F-70: Append-only run journal as the durable record
- Layer: Implementation
- Source: S6, https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ — "they're the durable record." (runs logged as an append-only event trace)
- Evidence: "Durability" section.
- Confidence: Medium — explicit quote; single source.

## F-71: Parked runs survive restarts
- Layer: Implementation
- Source: S6, https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ — "Parked runs survive restarts."
- Evidence: "Durability" section. Parked instances are rehydrated and their elicitations re-issued after restart (summary text).
- Confidence: Medium — explicit quote; single source.

## F-72: Author's claimed outcome of the pattern (no quote)
- Layer: Big Picture
- Source: S6, https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ — no verbatim quote retrieved.
- Evidence: "The outcome" section (fetch summary): the pattern removes glue code, makes workflows reviewable as YAML, and runs the same core across interactive and headless targets.
- Confidence: Low — author's own claim, summary text only, not verified.

---

## Cross-Source Notes (for the verification phase; not synthesis)

- F-35 to F-43 (S4) and F-21 (S3) state the same reducer premise that S6 restates (F-57). Repetition within one author is not independent corroboration.
- The series index (S3) and the intro (S4) both enumerate or count 13 patterns. Pattern-level details for each of the 13 are only in S3's excerpts; the linked pattern pages were not fetched.
- F-45 and F-46 (S5, list context as loop counter and comparison guards) and F-65 (S6, clearing trigger fields) describe two different loop-control techniques. Both are recorded as stated; they are not reconciled here.
- The idiom "Accumulate what you keep; consume what re-arms" appears in S3 (Supervisor / Router excerpt and the Idioms section), recorded under F-24. It is not in the S4 excerpts.

---

## Gaps

- G-1 (coverage, all six pages): WebFetch returned excerpts, not full text. Responses for S1, S2, S4, S5 and S6 each stated the model could not reproduce or extract the full article. S3 returned excerpts with no such notice. Full-article coverage is unknown for every page. Sections without a quote are recorded as Low (F-14, F-17, F-48, F-52, F-60, F-65, F-66, F-72).
- G-2 (quote fidelity): Quotes were returned by a summarizing model and were not checked against raw page HTML in this phase. Several returned quotes are cut mid-sentence or appear as fragments. The brief requires verbatim quotes; these are "as returned" and need a raw-HTML check during verification.
- G-3 (currency): Publication and last-updated dates were not present in any returned excerpt. Currency of each post has not been confirmed.
- G-4 (linked pages not fetched): Per plan rule, pages linked from the six URLs were not fetched (listed under Rejected Information). Any finding that depends on a linked pattern page (for example, the individual pattern articles behind S3's 13 entries) is unverified.
- G-5 (external reference): S6 names a GitHub project (aegis) as the implementation. It was not fetched and is not in the 18-URL list.

---

## Rejected Information

| # | Information | Source | Rejection Reason | Re-include If |
|---|---|---|---|---|
| R-1 | Linked sub-articles: agents-in-production-pt-1 to pt-4 (`/software/agents-in-production-pt-1` through `-pt-4`) | S1, https://bradmurry.com/software/agents-in-production-intro/ | Outside the 18 listed URLs; plan says record cross-links but do not follow them | Verification phase is authorised to fetch them, or the source list is extended |
| R-2 | Linked sub-articles: self-evolving-agents-pt-1 to pt-4 (`/software/self-evolving-agents-pt-1` through `-pt-4`) | S2, https://bradmurry.com/software/self-evolving-agents-intro/ | Same as R-1 | Same as R-1 |
| R-3 | Linked pattern articles: the 13 `...-implemented-via-reactive-reducer` pages (for example `/software/sequential-pipeline-pattern-implemented-via-reactive-reducer`) | S3, https://bradmurry.com/software/reactive-reducer-patterns/ and S4, https://bradmurry.com/software/reactive-reducer-patterns-intro/ | Outside the 18 listed URLs; they hold the per-pattern detail that the excerpts only summarise | Verification phase extends the list to cover them; they are the main gap for the 13-pattern catalogue |
| R-4 | Linked agents-in-production and self-evolving pages cross-referenced from S2 (`/software/agents-in-production-pt-3`, `/software/agents-in-production-pt-4`) | S2, https://bradmurry.com/software/self-evolving-agents-intro/ | Same as R-1 | Same as R-1 |
| R-5 | Aegis project repository (github.com/bradodarb/aegis) | S6, https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ | Not one of the 18 URLs; code repository not in the plan's source categories | Plan adds a code source category, or the verification phase needs the reference implementation |
| R-6 | Site navigation and profile links: /, /software, /about, /assets/resume.pdf, LinkedIn, GitHub profile | S1 through S6 | No pattern, concept or skill content | Not expected to be re-included |
| R-7 | Operational list "evals, rollouts, drift monitoring, cost budgets" (quoted only as a summary list) | S1, https://bradmurry.com/software/agents-in-production-intro/ | Present only in fetch-summary text, not a verbatim quote; the plan requires quoted evidence | A verbatim passage is retrieved from the full article (see G-1) |
| R-8 | Series-part titles (Part 1 to Part 4) beyond the link labels | S1, https://bradmurry.com/software/agents-in-production-intro/ | Only link labels returned; no article content for the parts | Part pages are fetched under the verification phase |
| R-9 | Pattern-level content for the 13 reactive-reducer patterns beyond the excerpt-level failure-point quotes (for example, specific mechanics per pattern) | S3, https://bradmurry.com/software/reactive-reducer-patterns/ | Not returned by the fetch excerpts; reconstructing it would be memory-based | Full article or pattern pages are retrieved |

Summary: 9 rejected entries. Nothing in the 6 pages was rejected as off-topic. Rejections are linked sub-pages, the external repository, navigation, and summary-only content.

Not in this category (noticed, not gathered): none of the six pages pointed to the data/infra/ops posts (sparkctl, dsl-ai-partition, etc.) beyond the series index link set. Those belong to `bm-data-infra-ops`.

## Actor Relevance

Omitted. The plan lists no actors ("research is actor-agnostic").
