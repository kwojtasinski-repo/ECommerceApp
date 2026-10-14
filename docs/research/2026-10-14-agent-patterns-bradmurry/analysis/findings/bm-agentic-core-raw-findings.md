# Findings: bm-agentic-core-raw

Re-gather from FULL-TEXT local files. No synthesis, no recommendation. All quotes copied verbatim from the files (<=40 words; ellipses only in J1 and I-table cells marking elided text).

## Source key

| Key | File (in `analysis/sources-raw/`) | Original URL |
|---|---|---|
| [03] | 03-binding-llms-to-fsm-reactive-reducer.md | https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ |
| [04] | 04-walking-reentrant-hil-fsm.md | https://bradmurry.com/software/walking-a-reentrant-human-in-the-loop-fsm-step-by-step/ |
| [06] | 06-agents-in-production-intro.md | https://bradmurry.com/software/agents-in-production-intro/ |
| [07] | 07-self-evolving-agents-intro.md | https://bradmurry.com/software/self-evolving-agents-intro/ (page heading "The Agent That Builds Its Own Tools") |
| [08] | 08-reactive-reducer-patterns-intro.md | https://bradmurry.com/software/reactive-reducer-patterns-intro/ |

Notes: [03]/[04] are first-party articles about the author's "Aegis" FSM runtime (one author, single-source). [06]/[07] state that Parts 1-4 of their series are not in the 18-URL list. Every finding is single-source unless a second key is listed; per the plan, single-source claims are treated as at most Medium until verification, but the Confidence field below records only how explicit the text is (High = explicit in text).

---

# A. Reactive reducer: core rules and mechanism

## A1. Core idea: context is the single source of truth; the machine advances itself
- Source: [03] 03-binding-llms-to-fsm-reactive-reducer.md, section "The core idea"; URL above
- Evidence: "Context is the single source of truth. Actors (agents, humans, tools, the system) only ever contribute to context. Transitions are pure, guarded conditions over context, re-evaluated after every context change - and the machine advances itself."
- Confidence: High - explicit definition
- Layer: Modeling

## A2. No requestTransition: an agent's choice is a context write
- Source: [03], same section
- Evidence: "There is no requestTransition call. No sign-off tool. No "agent asks to advance" button. An agent's choice is itself a context write that a guard routes on."
- Confidence: High
- Layer: Modeling

## A3. Failure class the pattern claims to eliminate
- Source: [03], same section
- Evidence: "it exists to eliminate a whole failure class: the agent that won't or forgets to poke a gated edge, or the one that flails deciding whether it may transition."
- Confidence: High (claim by author; lineage stated as xstate/Redux)
- Layer: Big Picture

## A4. Problem framing: LLM as non-deterministic control structure
- Source: [03], section "A little background"
- Evidence: "we were asking a non-deterministic actor to be a deterministic control structure. An LLM is a fantastic contributor. It is a terrible state machine."
- Confidence: High
- Layer: Big Picture

## A5. Production failure scenario that motivates the pattern (glue code)
- Source: [03], section "A little background"
- Evidence: "A human sign-off sits in a queue for two days while the whole run lives in volatile memory, waiting to be forgotten by a process restart."; "That glue code becomes the actual product. And it's where everything breaks."
- Confidence: High
- Layer: Big Picture

## A6. Consequence 1: legality is structural, not evaluated
- Source: [03], "Three consequences" list
- Evidence: "The reducer only ever fires declared edges. If an edge isn't in the YAML, it can't happen - there's no fire-time legality check to author, test, or bypass."
- Confidence: High
- Layer: Modeling

## A7. Consequence 2: the reducer is pure; elicitation is reported out, never blocking
- Source: [03]
- Evidence: "It never touches the bus and never blocks. Elicitation (the human modal, the agent summon) is reported out as pending work, and a human's answer re-enters as an ordinary context write."
- Confidence: High
- Layer: Modeling

## A8. Purity lets one core lower to headless targets
- Source: [03]
- Evidence: "That purity is what lets the identical core lower to headless targets (Mutiny, Flink) - only the effect sink and the async-resume transport differ."
- Confidence: High
- Layer: Implementation

## A9. Consequence 3: parking is a first-class state
- Source: [03]
- Evidence: "A state waiting on a human or a delegate agent doesn't spin, time out, or guess. It parks, reports what it's waiting on, and a later contribution re-enters as a clean write plus a re-run."
- Confidence: High
- Layer: Modeling

## A10. Three rules of the reactive reducer (series-intro formulation)
- Source: [08] 08-reactive-reducer-patterns-intro.md, section "The reactive reducer"
- Evidence: "context is the single source of truth."; "Agents advance the machine by writing context - never by requesting a transition."; "A microstep loop fires the first eligible, guard-passing edge until the machine parks."
- Confidence: High (three rules quoted from three list items)
- Layer: Modeling

## A11. Handoff is a context write; no transcripts
- Source: [08], rule 1
- Evidence: "There are no handoff messages, no transcripts, no "the agent summarized it." The handoff is a context write."
- Confidence: High
- Layer: Modeling

## A12. "The machine moves itself. Actors only ever contribute values."
- Source: [08]; also [03] ("Context is the only door in.")
- Evidence: "The machine moves itself. Actors only ever contribute values."
- Confidence: High
- Layer: Modeling

---

# B. Microstep loop and loop bounding

## B1. Microstep loop: four steps (eligibility, routing, fire, stop)
- Source: [03], section "The machine: a microstep loop"
- Evidence: "Eligibility - the current state's edges are eligible only when all of its required requires are satisfied: the named context field is present and every validate predicate passes. Not satisfied → the machine parks"
- Confidence: High
- Layer: Modeling

## B2. Routing: first declared edge whose guards pass; no passing edge parks
- Source: [03]
- Evidence: "among the eligible edges, the first one in declaration order whose guards all pass fires. An edge with no guards is unconditional. No edge passes → park."
- Confidence: High
- Layer: Modeling

## B3. Fire order: exit, onTransition, move, entry
- Source: [03]
- Evidence: "run the source state's exit actions, then the edge's onTransition actions, then move to the target, then run the target's entry actions. Then repeat from step 1 in the new state."
- Confidence: High
- Layer: Implementation

## B4. Stop conditions and microstep cap (128) and no-progress guard
- Source: [03]
- Evidence: "Stop - when a state parks, a terminal state is reached, the microstep cap (128) is hit, or the machine re-enters a state with unchanged context (the no-progress guard)."
- Confidence: High
- Layer: Implementation

## B5. Loop contains no agent identity, turn-taking, waiting, or I/O
- Source: [03], after the Java listing
- Evidence: "Notice what's not in that loop: no switch on agent identity, no "who's turn is it", no waiting, no I/O."
- Confidence: High
- Layer: Modeling

## B6. Two backstops make bad loops visible config bugs
- Source: [03]
- Evidence: "a badly authored loop is a config bug you see immediately, not a runaway process. Oscillation is caught, not suffered."
- Confidence: High
- Layer: Implementation

## B7. Determinism claim: same (state, context) gives same edge; journaled replay
- Source: [08], "Determinism" bullet
- Evidence: "Re-run the same (state, context) and you get the same edge. The run is a sequence of (state, context-write, edge-fired) records - a complete, journaled replay."
- Confidence: High
- Layer: Big Picture

## B8. Every loop bounded by verdict enum, policy.maxIterations, no-progress guard
- Source: [08], "Bounded tokens" bullet
- Evidence: "every loop - the rework loop, the debate, the planner/executor re-plan, the refinement cycle - is bounded by a verdict enum, policy.maxIterations, and the reducer's no-progress guard."
- Confidence: High
- Layer: Modeling

## B9. Bounded tokens: per-turn injection is CONTEXT block plus objective
- Source: [08]
- Evidence: "Context cost is roughly constant in the number of fields, not the sum of the agents' outputs."; "capped per value"
- Confidence: High
- Layer: Big Picture

## B10. Budget derived from data, not a counter
- Source: [04] 04-walking-reentrant-hil-fsm.md, section "Why the loop keeps its history"
- Evidence: "The revision budget here is size(context.drafts) < 3. The counter is the data, so it cannot disagree with the history it is counting."
- Confidence: High
- Layer: Modeling

## B11. Policy block in the DSL (exchange caps, checkpointing)
- Source: [03] YAML; [04] YAML (identical)
- Evidence: "maxExchangesPerStage: 30"; "maxExchangesPerAgentPerStage: 15"; "checkpointEvery: 5"
- Confidence: High (values as shown in the example YAML; not explained further in text)
- Layer: Implementation

---

# C. Parking, elicitation, human-in-the-loop

## C1. Ownership is derived from requires, not an owner field
- Source: [03], DSL section
- Evidence: "There is no owner or actor field. Ownership is derived: if all of a state's requires are from: agent it's a work state; if any requirement comes from a {user} or another {agent}, the state parks and waits."
- Confidence: High
- Layer: Modeling

## C2. requires is the I/O contract; the from selector
- Source: [03], DSL section (table)
- Evidence: "requires is the I/O contract. Each requirement names a context field and who contributes it. The from selector is the only "new" concept in the DSL - and it's data, not logic"
- Confidence: High
- Layer: Modeling

## C3. from selector forms (agent, user, agent peer via A2A, tool, system)
- Source: [03], table
- Evidence: "from: { user: brad } | human-in-the-loop - raised as a modal; parks the machine"; "from: { agent: reviewer } | a peer agent is summoned (A2A); parks the machine"; "from: system | produced automatically by the system"
- Confidence: High
- Layer: Modeling

## C4. Worked trace: review parks; no polling
- Source: [03], "A worked example"
- Evidence: "The loop stops and reports the pending elicitation; the runtime summons the reviewer over A2A. No one is polling. No one is asking."
- Confidence: High
- Layer: Modeling

## C5. A modal is just a UI over a context write
- Source: [04], Step 4
- Evidence: "That's the entire elicitation mechanism: a modal is just a UI over a context write."
- Confidence: High
- Layer: Modeling

## C6. Elicitation target resolved by template at state entry; enum pool validates
- Source: [04], Step 3
- Evidence: "from: { user: "{{ context.reviewer }}" } renders against live context when the state is entered"; "a value outside it is rejected at the write rather than discovered later as a page nobody answers."
- Confidence: High
- Layer: Modeling

## C7. Two requirements from the same human become one modal; form derived from schema
- Source: [04], Step 3
- Evidence: "The form is derived from the context schema, not from the requirement - the requirement only says who, and optionally overlays a label."
- Confidence: High
- Layer: Implementation

## C8. blocking: true is a presentation hint only
- Source: [04], Step 3
- Evidence: "this changes nothing about the machine - the elicitation is still asynchronous, the machine is still parked, and the answer still arrives whenever it arrives."
- Confidence: High
- Layer: Implementation

## C9. Agentless derived state (from: system with CEL extract); derive-then-use to avoid a race
- Source: [04], Step 2
- Evidence: "auto-resolution is asynchronous, so deriving the value in the same state that consumes it would be a race. Derive first, then use"
- Confidence: High
- Layer: Modeling

## C10. Zero-token system state
- Source: [04], Step 2
- Evidence: "Zero tokens, one journal entry."
- Confidence: High
- Layer: Implementation

## C11. Non-terminal abort state gives the agent one clean turn; enum enforces acknowledgement
- Source: [04], Path C
- Evidence: "ack is an enum with a single legal value, so "acknowledged" is enforced by the schema rather than by hoping the agent types the right token"
- Confidence: High
- Layer: Modeling

## C12. Interactive vs headless: pause, inspect store, resume
- Source: [08], "Two ways to run it"
- Evidence: "because every write is journaled, you can pause, inspect the store, and resume."
- Confidence: High
- Layer: Implementation

---

# D. Guards (CEL), routing, joins, failure semantics

## D1. Guards are plain CEL, pure predicates
- Source: [03], DSL section
- Evidence: "Guards are plain CEL over context. No bespoke condition syntax, no when: keyword, no scalar guard:. Routing is a guard, and a guard is a pure predicate - no wall-clock, no randomness, no I/O."
- Confidence: High
- Layer: Modeling

## D2. Effects only in actions; a guard may never cause an effect
- Source: [03]
- Evidence: "Effects live only in actions (entry / exit / onTransition): assign (literal context writes), emit, send, spawn (child FSM), inject (a template as a system message), restrict (tool restrictions). A guard may never cause an effect."
- Confidence: High
- Layer: Modeling

## D3. Routing inspectable, side effects in one auditable place
- Source: [03]
- Evidence: "That separation is the whole ballgame - routing stays inspectable and side effects stay in one auditable place."
- Confidence: High
- Layer: Big Picture

## D4. Guards evaluated by the general guardrail engine at the transition site
- Source: [03], "Guards: routing is data"
- Evidence: "evaluated by the general guardrail engine at the TRANSITION site - the same engine that gates tool calls and delegation, not a bespoke FSM syntax."
- Confidence: High
- Layer: Implementation

## D5. Joins are predicates over has(...), no N-of-M primitive
- Source: [03]
- Evidence: "Don't invent an "N-of-M" primitive or a count field. A join is a guard over has(...)."
- Confidence: High
- Layer: Modeling

## D6. required: false lets the guard be the sole join
- Source: [03]
- Evidence: "mark the fan-out requirements required: false. They're still elicited on entry; they just don't block edge eligibility, so the guard alone decides."
- Confidence: High
- Layer: Modeling

## D7. Panel-review example join
- Source: [03]
- Evidence: "three reviewer agents plus a human sign-off, all required: false, joined by (security && perf && human) || legal_override."
- Confidence: High
- Layer: Modeling

## D8. A throwing guard does not fire (hazard-free)
- Source: [03]
- Evidence: "A guard that throws is treated as not passing - the edge simply doesn't fire. Never a crash, never a half-applied transition."
- Confidence: High
- Layer: Implementation

## D9. validate predicates catch bad values
- Source: [03], "The outcome"
- Evidence: "The failure modes you fight are "the agent produced a bad value" - catchable with validate predicates - instead of "the agent did a thing that wasn't in the plan", which is uncatchable."
- Confidence: High
- Layer: Big Picture

## D10. Workflow is reviewable data (YAML in git; CEL routing)
- Source: [03], "The outcome"
- Evidence: "A YAML file in git. Scannable diffs. The routing logic is a handful of CEL expressions, not a tangle of if-statements across five services."
- Confidence: High
- Layer: Big Picture

## D11. Agent sees the store each turn (CONTEXT block, templating)
- Source: [03], "What the agent actually sees"
- Evidence: "Every turn, the runtime injects a CONTEXT (workflow data set so far): block listing every set field and its value, alongside the current state's objective and tool steering."
- Confidence: High
- Layer: Implementation

## D12. Agent job shrinks to producing a value
- Source: [03]
- Evidence: "The agent's job shrinks to the one thing LLMs are good at: produce a value, well, for the current objective. Everything else - routing, waiting, legality, memory - is the machine's job."
- Confidence: High
- Layer: Big Picture

---

# E. Consume-the-trigger vs accumulate

## E1. Consume the trigger: clear the routing context so the edge fires once
- Source: [03], "A worked example"
- Evidence: "Clearing the triggering context in an action is how you get "fire once when X happened" semantics - and it's the difference between a rework loop and an infinite one."
- Confidence: High
- Layer: Modeling

## E2. Why: machine runs to stable, so a left-set trigger re-fires
- Source: [03]
- Evidence: "Because the machine runs to stable, leaving verdict == 'changes' set would immediately re-fire the same edge."
- Confidence: High
- Layer: Modeling

## E3. No-progress guard is the backstop if consume is forgotten
- Source: [03]
- Evidence: "(The no-progress guard is your backstop if you get this wrong.)"
- Confidence: High
- Layer: Implementation

## E4. Accumulate (list context) instead of consume
- Source: [04], "Why the loop keeps its history"
- Evidence: "Nothing is ever cleared to make the loop work."; "The drafts, the verdicts and the reasons all accumulate, and the loop reads its own history to decide what to do next."
- Confidence: High
- Layer: Modeling

## E5. Cost of consume: destroys revision history
- Source: [04]
- Evidence: "Every pass throws away the previous draft and the human's reasoning. By the time a review loop finishes, the revision history - which is the interesting part of a review loop - is unrecoverable."
- Confidence: High
- Layer: Modeling

## E6. Writes to list fields append, not replace
- Source: [04], Step 1
- Evidence: "Because drafts is a list, that write appends; it doesn't replace."
- Confidence: High
- Layer: Implementation

## E7. Under accumulate, presence stops being a signal; guards become size comparisons
- Source: [04]
- Evidence: "has(context.verdicts) is true from the first answer onward and never goes back to false, so every test in the loop has to become a comparison - and on both sides."
- Confidence: High
- Layer: Modeling

## E8. Consequence of a one-sided comparison: stale verdict silently reused
- Source: [04]
- Evidence: "Get only the first and the machine runs draft → review → draft inside a single advance, silently reusing the previous round's verdict."
- Confidence: High
- Layer: Modeling

## E9. Stale-verdict risk on reentry, guarded by size equality
- Source: [04], YAML comment and Path B
- Evidence: "Without the comparison the machine would route on a stale verdict without ever raising the modal."
- Confidence: High
- Layer: Modeling

## E10. Index alignment requires reasons to be mandatory
- Source: [04]
- Evidence: "If a human could skip it, reasons and verdicts would drift out of alignment and reasons[i] would stop being the note for verdicts[i]."
- Confidence: High
- Layer: Modeling

## E11. Only the routing field is consumed in the accumulate design
- Source: [04], Path B
- Evidence: "That's the only thing consumed in the entire loop, and it isn't an artifact - it's the routing decision."; "an auto-resolved requirement only fires when its field is unset"
- Confidence: High
- Layer: Modeling

## E12. Declared-but-unwritten lists bind as empty collection; scalars deliberately not seeded
- Source: [04], Step 0
- Evidence: "a declared-but-unwritten list binds as an empty collection in guard expressions, not as an absent key."; "Scalars are deliberately not seeded the same way"
- Confidence: High
- Layer: Implementation

---

# F. Durability, envelope, journal

## F1. Parked workflow cannot live in volatile memory
- Source: [03], "Durability" section
- Evidence: "A workflow that parks on a human can wait days. It cannot live only in volatile memory."
- Confidence: High
- Layer: Big Picture

## F2. Immutable Envelope, copy-on-write, version-CAS, lock-free composition
- Source: [03]
- Evidence: "lives behind a single immutable Envelope, updated copy-on-write (a version-CAS on an AtomicReference), so concurrent updates compose without a lock."
- Confidence: High
- Layer: Implementation

## F3. Envelope contents: portable state only
- Source: [03]
- Evidence: "current state, iteration counter, guard-relevant context, and transition history"; "no live agent references, no bus handles, no sockets. Those get reconstructed per target."
- Confidence: High
- Layer: Implementation

## F4. Claim-checking (ClaimRef) for blob/ref fields
- Source: [03]
- Evidence: "A context field declared blob/ref gets offloaded to a PayloadStore and carried in the envelope as a small ClaimRef (store, key, checksum, TTL)."
- Confidence: High
- Layer: Implementation

## F5. Run journaling: snapshot plus append-only causal trace
- Source: [03]
- Evidence: "the latest envelope snapshot plus an append-only causal event trace (created, context, transition, elicitation, response, terminal)."
- Confidence: High
- Layer: Implementation

## F6. Runs are the durable record, replayable for evals
- Source: [03]
- Evidence: "Runs are not deleted at session cleanup; they're the durable record, inspectable and replayable for evals."
- Confidence: High
- Layer: Implementation

## F7. Park store: persisted parked record; restart re-issues the elicitation; no external workflow service
- Source: [03]
- Evidence: "persisted as (interventionId, Envelope, pending elicitation)."; "RPC-free, no external workflow service"
- Confidence: High
- Layer: Implementation

## F8. Authoring rule: durable values belong in context; session counters are re-derived
- Source: [03]
- Evidence: "if a value must be durable and inspectable, it belongs in context (the envelope). Session-scoped counters - exchange budgets, drift, corrections - are re-derived and never need to survive."
- Confidence: High
- Layer: Modeling

## F9. Terminal run purge via policy.onTerminal
- Source: [03], worked example step 3
- Evidence: "if policy.onTerminal says so, claim-checked blobs get purged."
- Confidence: High
- Layer: Implementation

## F10. Interactive runner vs compiled Mutiny/Flink; same FSM
- Source: [08], "Two ways to run it"
- Evidence: "The FSM doesn't change - the runtime does."; "the park is a durable, checkpointed state; the guards are the routing predicates."
- Confidence: High
- Layer: Implementation

## F11. Pattern separates coordination logic from execution substrate
- Source: [08]
- Evidence: "the coordination logic is separate from the execution substrate."
- Confidence: High
- Layer: Big Picture

---

# G. Series intro: failure modes and thesis ([08])

## G1. Three recurring failure modes with one root cause
- Source: [08], intro
- Evidence: "Non-deterministic outcomes."; "Poor user experiences."; "Token budgets exploding."; "the agents are doing the control flow."
- Confidence: High
- Layer: Big Picture

## G2. Token growth mechanism in transcript-passing designs
- Source: [08]
- Evidence: "Every agent carries the growing transcript of every other agent. Context grows with the number of hops, the number of specialists, the number of rounds."
- Confidence: High
- Layer: Big Picture

## G3. Prompting cannot make a model's control flow deterministic
- Source: [08]
- Evidence: "You can prompt it to be careful. You can't make it deterministic."
- Confidence: High
- Layer: Big Picture

## G4. Series thesis: implement each orchestration pattern as a reducer
- Source: [08]
- Evidence: "The pattern is the shape of the coordination. The reducer is the thing that makes the shape exact."
- Confidence: High
- Layer: Big Picture

## G5. Pattern list (13) and grouping
- Source: [08], "What this series covers"
- Evidence: "Flow: Sequential Pipeline, Parallel Fan-Out / Fan-In"; "Authority: Supervisor / Router, Hierarchical Manager / Worker"; "Reasoning: Debate / Consensus, Quorum / Voting, Generator / Critic / Refiner"
- Confidence: High (other groups: Delegation = Peer Handoff/Swarm, Auction/Contract-Net, Dynamic Team Formation; Planning = Planner/Executor; Coordination = Blackboard/Shared-State, Event-Driven/Pub-Sub)
- Layer: Big Picture

---

# H. Agents in Production intro ([06])

## H1. Incident framing: model fine, system around it was not
- Source: [06] 06-agents-in-production-intro.md
- Evidence: "It did exactly what it was built to do, and it did it twice."; "The model was fine. The system around it was not."
- Confidence: High
- Layer: Big Picture

## H2. Distributed-systems lessons carry over; a model is probabilistic, non-replayable, expensive
- Source: [06]
- Evidence: "a model is a new kind of component: probabilistic, non-replayable, and expensive."
- Confidence: High
- Layer: Big Picture

## H3. Operational muscle for models
- Source: [06]
- Evidence: "evals before a change ships, staged rollouts, drift monitoring, and a cost budget that gets watched as closely as correctness."
- Confidence: High
- Layer: Big Picture

## H4. Deterministic shell thesis
- Source: [06]
- Evidence: "we make autonomous agents reliable the same way we have always made distributed systems reliable, by putting the nondeterministic part inside a deterministic shell."
- Confidence: High
- Layer: Big Picture

## H5. Claim 1: an agent in production is a distributed system
- Source: [06], "What the series argues"
- Evidence: "It has state, failure, concurrency, retries, coordination, and observability."
- Confidence: High
- Layer: Big Picture

## H6. Claim 2: control flow lives in states/guards/transitions with durable position, not the prompt
- Source: [06]
- Evidence: "The model contributes a value, and the machine decides what that value means for the next step."; "It does not live in the prompt."
- Confidence: High
- Layer: Big Picture

## H7. Claim 3: the shell is operated; capacity inputs from day one; versioned definitions; on-call
- Source: [06]
- Evidence: "the architecture that works at 10,000 concurrent workflows is the one you need at 10, so concurrency limits, queue depth, and cost per workflow are capacity inputs from day one."
- Confidence: High
- Layer: Big Picture

## H8. Workflow definition is a versioned, canaried, rollback-able artifact
- Source: [06]
- Evidence: "The workflow definition is a versioned artifact that gets rolled out, canaried, and rolled back like any other change, and someone is on call when a workflow gets stuck."
- Confidence: High
- Layer: Big Picture

## H9. Series structure: 12 articles in three acts; Act II/III "coming"
- Source: [06]
- Evidence: "Act II proves it technically. Idempotent effects, event journaling, replay and recovery, backpressure, and durable human review."; "Those are coming."
- Confidence: High
- Layer: Big Picture

## H10. Series format includes honest analogy-breakdown
- Source: [06]
- Evidence: "shows the diagram, builds a small working example, and then gets honest about where the analogy breaks down."
- Confidence: High
- Layer: Big Picture

## H11. Target audience stakes
- Source: [06]
- Evidence: "If you are putting agents in front of anything that touches a customer, changes a record, or moves money, start with Part 1."
- Confidence: High
- Layer: Big Picture

---

# I. Self-evolving agents ([07])

## I1. Premise: task + measure of success + power to build tools/workflows
- Source: [07] 07-self-evolving-agents-intro.md
- Evidence: "A self-evolving agent harness lets you point an agent at a general problem and have it create novel solutions by iterating on its own feedback."
- Confidence: High
- Layer: Big Picture

## I2. Human role: task and yardstick
- Source: [07]
- Evidence: "My job becomes three things: give the task, give the yardstick (the metrics that will drive discovery), approve the dangerous operations."
- Confidence: High
- Layer: Big Picture

## I3. Loop: RUN, MEASURE, DISCOVER, BUILD
- Source: [07], "The discovery loop"
- Evidence: "The agent runs four phases, over and over"; "RUN."; "MEASURE."; "DISCOVER."; "BUILD."
- Confidence: High
- Layer: Modeling

## I4. RUN: the worker is deliberately dumb
- Source: [07]
- Evidence: "If the worker is smart, you can't tell whether the workflow works or the worker is compensating. So the worker is dumb on purpose."
- Confidence: High
- Layer: Modeling

## I5. BUILD: deterministic tool plus FSM workflow plus gate; previous version archived
- Source: [07]
- Evidence: "It writes a tool for the deterministic part. It designs a workflow (a finite state machine) for the part that needs judgment. It adds a gate that makes the workflow able to fail."; "the previous version is archived. A bad round is recoverable."
- Confidence: High
- Layer: Modeling

## I6. Stop criterion: plateau across three consecutive runs
- Source: [07]
- Evidence: "the metric it set out to improve has plateaued across three consecutive runs."
- Confidence: High
- Layer: Modeling

## I7. Discovery is data-driven, not intuition-driven
- Source: [07]
- Evidence: "The discovery is data-driven, not intuition-driven."
- Confidence: High
- Layer: Modeling

## I8. Three signals: efficiency, correctness, quality
- Source: [07], table in "Three signals"
- Evidence: "Efficiency | How the process actually ran | getFsmRun | Fact (the run journal)"; "Correctness | ... | A deterministic recompute + diff | Fact (checkable)"; "Quality | ... | evalById (LLM critic) | Opinion (a hint, not a score)"
- Confidence: High
- Layer: Modeling

## I9. "One signal lies"; need all three; state numbers before interpreting
- Source: [07]
- Evidence: "Most systems reach for one signal and call it done. But one signal lies."; "must state the numbers before interpreting them."
- Confidence: High
- Layer: Modeling

## I10. Rule: facts before opinion
- Source: [07]
- Evidence: "The rule is iron: facts before opinion."
- Confidence: High
- Layer: Modeling

## I11. Efficiency signal: authoritative on how the process ran, not whether it worked
- Source: [07]
- Evidence: "It's authoritative on how the process ran, not whether it worked."
- Confidence: High
- Layer: Modeling

## I12. Correctness signal: deterministic recompute and diff drives next discovery
- Source: [07]
- Evidence: "It recomputes the expected output from the raw input and diffs it against what the agent produced. It returns approved or rejected: <reasons>."
- Confidence: High
- Layer: Modeling

## I13. Quality signal (LLM critic) is the weakest; drifts toward "looks thorough"
- Source: [07]
- Evidence: "It's the weakest signal: it drifts toward "looks thorough." Treat it as a hint."
- Confidence: High
- Layer: Modeling

## I14. Signals can disagree
- Source: [07]
- Evidence: "A run can be clean in the journal, pass the correctness check, and still be expensive or incomplete. It can have a perfect critic score and still be wrong."
- Confidence: High
- Layer: Modeling

## I15. Clean-journal trap: no gate means cannot fail
- Source: [07], "A clean journal is not a good outcome"
- Evidence: "A workflow with no quality gate cannot fail, so a clean journal proves nothing about the output."
- Confidence: High
- Layer: Big Picture

## I16. Journal records process only; cannot see design flaws
- Source: [07]
- Evidence: "the journal only records process, and the process ran exactly as designed. The flaw was in the design, and a journal can't see a flaw in the design."
- Confidence: High
- Layer: Big Picture

## I17. Discipline: never accept clean journal as evidence; make workflow able to fail
- Source: [07]
- Evidence: "never accept a clean journal as evidence of a good outcome. Build the gate. Make the workflow able to fail."
- Confidence: High
- Layer: Modeling

## I18. Two halves of every task: deterministic core vs variable layer
- Source: [07], "Every task has two halves"
- Evidence: "The deterministic core. The part that's voluminous and rule-based. Parsing, aggregation, profiling, dedupe, normalization."; "The variable layer. The part that's genuinely variable and requires judgment."
- Confidence: High
- Layer: Modeling

## I19. Why tool for core: fewer tokens, outside context window, reproducible
- Source: [07]
- Evidence: "Fewer tokens, because the tool processes the volume outside the context window. Faster. No context-window ceiling. Reproducible."
- Confidence: High
- Layer: Modeling

## I20. Why LLM for variable layer: a static tool would be overfit
- Source: [07]
- Evidence: "A tool would be overfit and would fail on the new cases it hasn't seen. For this half, the LLM's generalization is the feature, not the bug."
- Confidence: High
- Layer: Modeling

## I21. Design principle: each in its lane, with a gate on the boundary
- Source: [07]
- Evidence: "the agent learns to put the deterministic tool in its lane and the LLM in its lane, and to build a gate that checks the boundary between them."
- Confidence: High
- Layer: Modeling

## I22. Discipline 1: one change at a time
- Source: [07], "The disciplines"
- Evidence: "If you change three things and a metric improves, you've learned nothing. One change per round keeps the signal clean."
- Confidence: High
- Layer: Modeling

## I23. Discipline 2: check before you build (do measurements name the gap?)
- Source: [07]
- Evidence: "do the measurements actually name this gap?"; "If the measurements don't show a gap, don't build."
- Confidence: High
- Layer: Modeling

## I24. Discipline 3: report the regression (version archived, recovery available)
- Source: [07]
- Evidence: "If a change makes a metric worse, the agent says so explicitly. The version is archived, so recovery is always available."
- Confidence: High
- Layer: Modeling

## I25. Discipline 4: report the ceiling after three flat rounds
- Source: [07]
- Evidence: "If three rounds in a row show no measurable improvement, the agent stops. It reports the ceiling"
- Confidence: High
- Layer: Modeling

## I26. "A loop with no rules is just thrashing"
- Source: [07]
- Evidence: "A loop with no rules is just thrashing."
- Confidence: High
- Layer: Modeling

## I27. Reported empirical case: disease-etl, five archived versions
- Source: [07], "This is real"
- Evidence: "That workflow went through five versions. The agent built version 1, measured it, discovered what was breaking, built version 2."
- Confidence: High (author's report; no numbers in this intro)
- Layer: Implementation

## I28. Discovered error classes across versions (hand-serialization, drifting fields, gate checking wrong thing)
- Source: [07]
- Evidence: "That's an error class: dropped keys, misformatted nesting, fields lost in the copy."; "context fields drift."; "the gate was checking the wrong thing."
- Confidence: High
- Layer: Implementation

## I29. Four example parts (deterministic core / variable layer pairs)
- Source: [07], "The four examples"
- Evidence: "log_triage: parse, dedupe by signature, aggregate counts and trends"; "A deterministic blocking tool | Fuzzy resolution of the ambiguous matches"
- Confidence: High
- Layer: Implementation

## I30. Per-part arc: baseline by hand, eval names gap, deterministic core, gated variable layer, tune to optimum
- Source: [07]
- Evidence: "a round-zero baseline where the agent does the work by hand, an eval that names the gap, a deterministic core that crushes the volume, a variable layer with a quality gate, and a tune-to-optimum"
- Confidence: High
- Layer: Modeling

## I31. Self-evolving series builds on the shell; the agent builds the shell itself
- Source: [07]
- Evidence: "the LLM sits inside a deterministic shell, and workflows are state machines, not task lists. Here the shell itself is built and tuned by the agent"
- Confidence: High
- Layer: Big Picture

---

# J. Author limitations, caveats, and self-corrections (logged separately for verification)

## J1. Cross-turn convergence is NOT covered by the no-progress guard
- Source: [04], Path B
- Evidence: "the reducer's no-progress guard stops oscillation within a single advance() call... but it does not span turns. It will not notice a review cycle going round four times with slightly different text each time."
- Confidence: High
- Layer: Implementation
- Note: Qualifies [03] B6 ("Oscillation is caught") and [08] B8 (loops bounded by no-progress guard). Per [04], cross-turn convergence is "the guard on the data plus policy.maxIterations, and nothing else."

## J2. Author's own earlier version was broken (cycle counter silently dropped)
- Source: [04], "Why the loop keeps its history"
- Evidence: "The old version of this FSM carried a cycle: { type: int, default: 0 } field to bound the loop. It never worked - default: isn't part of the context schema, so the key was silently dropped"
- Confidence: High
- Layer: Modeling

## J3. Author states his earlier design would differ ("six months ago")
- Source: [04]
- Evidence: "That's the whole difference between this machine and the version I'd have written six months ago, and it changes how every guard in it is written."
- Confidence: High
- Layer: Modeling

## J4. Consume idiom is "a real idiom" but destructive; superseded in the later article
- Source: [04] vs [03]
- Evidence: "That works, and it's a real idiom - the trigger has to be consumed or the edge re-fires immediately when the machine runs to stable."
- Confidence: High
- Layer: Modeling
- Note: [03] presents consume-the-trigger as the rework mechanism; [04] presents accumulate as preferred where history matters. Not framed by the author as a contradiction.

## J5. Authoring pitfall: canTransition must list every agent-driven state, else permanent stall
- Source: [03], worked example
- Evidence: "Omit a reentrant state and no agent is authorized there - a permanent stall. The runtime now yells at you when this happens, but you should author it right the first time."
- Confidence: High
- Layer: Implementation

## J6. Errored guard silently never fires (seeding caveat)
- Source: [04], Step 0
- Evidence: "an errored guard is treated as "does not fire" - so without that seeding, size(context.drafts) > size(context.verdicts) would silently never fire"
- Confidence: High
- Layer: Implementation
- Note: Complements D8 (throwing guard is "hazard-free"): same behavior is a silent-failure risk for authors.

## J7. Clean journal planted as a deliberate trap (author disclosure)
- Source: [07]
- Evidence: "I didn't discover the clean-journal trap by accident. I planted it."
- Confidence: High
- Layer: Big Picture

## J8. Round-zero output looked right but the journal was meaningless
- Source: [07]
- Evidence: "A clean journal is not a good outcome. It's the absence of a check."
- Confidence: High
- Layer: Big Picture

## J9. Quality (LLM critic) signal explicitly weak
- Source: [07]
- Evidence: "It's the weakest signal: it drifts toward "looks thorough.""
- Confidence: High
- Layer: Modeling

## J10. Series promises "honest" analogy-breakdown sections; Acts II/III not yet published; claim "all true"
- Source: [06]; [07]
- Evidence: [06] "Those are coming."; [07] "This series is all true. Not a thought experiment."
- Confidence: High
- Layer: Big Picture
- Note: [07] self-attestation of reality, no third-party verification in file; [06] future parts unpublished at time of capture.

## J11. Anecdotal/opinion elements by the author (expressed preference, not evidence)
- Source: [07], "Until next time"
- Evidence: "I think you'll find it's not just good enough." / "That's the power of a self-evolving harness."
- Confidence: High (that the statement was made); the underlying claim is opinion
- Layer: Big Picture

## J12. Production claim stated without numbers in these intros
- Source: [03]
- Evidence: "I've been running this pattern against real multi-agent workflows - drafting, delegated review, panel sign-off - and the glue code is gone."
- Confidence: High (as an author claim); no metrics, scale, or incident data given in the file
- Layer: Big Picture

---

## Declarative Conclusions
Not applicable (external sources, not human transcripts).

## Rejected Information

| # | Information | Source | Rejection Reason | Re-include If |
|---|---|---|---|---|
| 1 | Java listing of FsmReducer.advance (code detail beyond loop steps) | [03] | Already captured as B1-B6 mechanics; full code not needed in findings | Implementation fidelity of the loop is questioned |
| 2 | Full YAML of delegated-review and hil-review-reentrant machines | [03], [04] | Elements extracted individually (C, D, E); whole YAML left in source files | A worked example is required in the catalog |
| 3 | disease-etl v1 and v5 extract-state YAML text | [07] | Only the lesson (I28) captured | Detailed diff evidence is needed |
| 4 | Parts 1-4 of Agents in Production and Self-Evolving series (content) | [06], [07] | Not in the 18-URL list; only the intro text is available | The parts are added to the source list |
| 5 | Mermaid diagrams (flattened by paste) | [03]-[08] | Labels flattened; text of the article carries the same content | Diagram structure is needed |
| 6 | Series-narrative filler ("Until next time", teasers of next parts) | [03], [06], [07], [08] | No analytical content | None expected |
| 7 | The specific example workflows' domain details (webhook retry, public-health data) | [03], [04], [07] | Illustrative only | A domain-specific analogy is requested |

Gaps noted: no numeric results (token, cost, scale, failure rates) appear in [03], [04], [06], [08]; [07] gives no convergence metrics in the intro (it defers them to Parts 1-4). Cross-links noted for verification: [06] and [07] both refer to Parts 1-4 of their series (not followed); [08] refers to the "series landing page" (not followed); [07] says Part 3 builds on "Deterministic Shell, Probabilistic Core" and Part 4 on "Stop Treating Agent Workflows Like Task Lists" (not followed).
