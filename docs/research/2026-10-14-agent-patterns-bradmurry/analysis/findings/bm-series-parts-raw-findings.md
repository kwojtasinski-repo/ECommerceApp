# Findings: bm-series-parts-raw

Scope for EVERY finding below: **scope: extended (outside original 18 URLs)**. Sources are pasted the full text of the articles in (sources: SOURCES.md) (called "file NN" below):
- 22 = Self-evolving agents Part 1 (log triage); 23 = Part 2 (data quality); 24 = Part 3 (ticket mining); 25 = Part 4 (entity resolution)
- 26 = Agents in Production (AIP) Part 1 (distributed systems); 27 = AIP Part 2 (Kafka/Flink); 28 = AIP Part 3 (deterministic shell); 29 = AIP Part 4 (task lists vs FSM)

Conventions: Layers = Big Picture / Modeling / Implementation. Confidence High = the claim is stated explicitly in the quoted text (single-source, so cross-verification is a later phase). Quotes are verbatim, <=40 words. Where a table is transcribed, rows are copied exactly from the file. Files 28 and 29 omit the incident-triage YAML (editorial note in their headers); the verbatim YAML is in file 27. No synthesis or recommendation is made here.

---

# PART A. Agents in Production series (files 26-29)

## A1. Mapping table: distributed-systems concept -> agent analogue (file 26)
- Layer: Big Picture / Modeling
- Source: file 26. Quote: "an agent system is one of those systems now."
- Evidence (table transcribed, "Distributed systems concept | Agent system analogue"):
  - Node (long-lived process) | A workflow instance running on an agent runtime
  - Unreliable network | Model, tool, and agent-to-agent calls that time out or drop
  - Message loss | A tool call succeeds, but the response never reaches the agent
  - Duplicate delivery | A retry re-issues an effect that already happened
  - At-least-once semantics | What agent retries give you by default
  - Idempotent operation | An effect with a stable key: a page, an email, an order, a ticket
  - Durable state | Workflow state in a journal, not in a model's context window
  - Consistency | "What does the agent believe is true" versus what the journal says
  - Two-phase commit / saga | A multi-step effect (assess, contain, notify) with compensating actions
  - Checkpointing | Persisting enough state to resume a run after a restart
  - Backpressure | Throttling agent concurrency to what models, APIs, and humans can absorb
  - Coordination / leader election | Which agent is driving the workflow right now, and who may move it
  - Dead letter queue | Escalation to a human when an agent is stuck or uncertain
  - Clock and time | Workflows that span hours or days while a human reviews
  - Observability | Decision provenance: not just what the LLM called, but why the system did what it did
  - Capacity planning | A concurrency budget: how many workflows may run at once, sized against model throughput, API quotas, and human review capacity
  - Autoscaling | Worker pool sizing under queue depth: more workers where the backlog grows, not where it is empty
  - Cost accounting | Token spend as a per-effect line item: what a page, a ticket, or an incident actually costs, not just whether it succeeded
  - SLO / paging | Who is on call when the escalation queue backs up, and what metric moves before the queue does
- Confidence: High (explicit table)

## A2. Failure-to-pattern table (file 26)
- Layer: Big Picture
- Source: file 26 ("The whole thing, one idea")
- Evidence (table, "Failure | Systems name | Pattern that fixes it"):
  - Retry after a timeout pages twice | Duplicate delivery | Idempotency key on every effect
  - A dropped response looks like a failure | No distinction between failed and unknown | Timeouts are UNKNOWN, not FAILED; retry, do not assume
  - The agent forgets what it already tried | State in the model's head | Durable state: (state, context) journaled and re-hydrated
  - A multi-step incident half-happens | Partial effect | Saga with compensating actions
  - A retry storm burns tokens unbounded | No backpressure | Bounded retries plus a capacity plan
  - Nobody owns the stuck runs | No dead letter queue | An escalation queue with a named owner and a paging threshold
- Confidence: High

## A3. The motivating failure: timeout treated as failure -> 40 double-pages (file 26)
- Layer: Big Picture
- Source: file 26. Quote: "The agent was not broken. It was doing exactly what you taught it to do: when a call fails, retry. The problem is that \"the call failed\" was never true."
- Evidence: Paging API hangs, 30-second timeout, agent retries "deriving a fresh notification instead of reusing the first one"; "By the time anyone notices, it has done this forty times." First-reached fixes ("make the timeout longer", better model, careful prompt) "None of those touch the actual failure."
- Confidence: High

## A4. At-least-once + idempotency; the vocabulary list (file 26)
- Layer: Modeling
- Source: file 26. Quote: "Deliver at least once, and make the operation idempotent, so that a duplicate is harmless."
- Evidence: "INSERT is not idempotent. UPSERT is. SET x = 5 is. \"Page the on-call about incident 4021\" is not, until you give it a key that the paging system can use". Vocabulary bullets: at-least-once delivery; idempotency; effect journaling ("how you know what actually happened, when the network will not tell you"); compensating actions (saga); backpressure.
- Confidence: High

## A5. Idempotency key mechanics: derive once, reuse on retry, scope to incident not run (file 26)
- Layer: Implementation
- Source: file 26. Quote: "Every real-world effect (a page, an email, a rollback) is recorded with an idempotency key before it is attempted. A retry reuses the key."
- Evidence: Walkthrough: "The agent marks the attempt unknown, backs off, and retries with the same key. The paging API sees the key... and returns the original reference instead of sending a second one." FSM state `derive-key` has no agent: "The key is data, not a decision." Key derived by CEL `extract: "'notify-' + context.incidentId"`. Scoping: "a second run that picks up the same incident derives the same key, and the paging system deduplicates across runs, not just within one."
- Confidence: High

## A6. "A timeout is UNKNOWN, not FAILED" (file 26)
- Layer: Modeling / Implementation
- Source: file 26. Quote (in the `notify` state description): "A timeout is UNKNOWN, not FAILED: back off and try again with the SAME key. A duplicate is harmless."
- Evidence: Prose: "The moment you treat a timeout as a failure, you are one retry away from a double page." and "it is the most common authoring mistake I see." Python shape: the `except TimeoutError` branch "does not assume the notification failed. It backs off and tries again with the same key"; second attempt returns "either a new reference (the first one really did fail) or the original reference (the first one succeeded)". Retry bound: `size(context.attempts) < 3`; on exhaustion `escalate` with "Attempt ledger" and key so a human can check the paging system.
- Confidence: High

## A7. Journal / effect journal as source of truth; durable external state (file 26)
- Layer: Modeling
- Source: file 26. Quote: "State is external and durable. The current state and the context live in a store that survives a restart. The model is re-hydrated from it on every turn."
- Evidence: "The effect journal is the source of truth. If the process dies between notify and done, the next process reads the journal, sees the key and the pending attempt, and re-issues the same call." Also: "The effect journal, not the model's prose, is the thing you check."
- Confidence: High

## A8. Architecture rule: machine drives, model contributes (file 26)
- Layer: Modeling
- Source: file 26. Quote: "The machine drives, the model contributes. The agent proposes values and effects. A deterministic state machine decides what is legal, what is pending, and what is done."
- Confidence: High

## A9. Bounded loop counter as data (list-as-counter) (files 26, 27, 28, 29)
- Layer: Implementation
- Source: file 26. Quote: "attempts is a list in context: the tool appends one entry per attempt, so the counter is the data, and it cannot disagree with the ledger it is counting."
- Evidence: Same idiom for verify loop: file 27 "checks is a list in context: the tool appends one entry per verify pass, so the loop counter is data, not a variable that dies with the process." Re-entry edge "consumes its own trigger" via `assign: { verifyOutcome: "" }`; "When the budget is spent, the machine does not guess. It hands off to a human with the full history".
- Confidence: High

## A10. Where the analogy breaks down - distributed systems (file 26) [limitations, 6 items]
- Layer: Big Picture
- Source: file 26, section "Where the analogy breaks down". Author intro: "a good analogy taken too far is worse than no analogy at all."
- Evidence (one quote each):
  1. Probabilistic node. Quote: "A retry of a pure function returns the same answer. A retry of an LLM call can return a different one."
  2. Expensive retries. Quote: "A retry storm in a distributed system is an infrastructure problem. A retry storm in an agent system is a line item on your invoice." Three cost components named: tokens burned, latency inherited by on-call, "the human attention a spent retry budget eventually consumes at the escalation queue". Quote: "A runbook for an agent system needs a per-workflow cost ceiling, not just a retry ceiling."
  3. Stateless by default. Quote: "The durable state store is not an optimization. It is the only place the state exists at all."
  4. Humans are the slowest node. Quote: "An agent workflow that parks on a human for a week is the normal case."
  5. Network has no consistency guarantees. Quote: "There is no replication, no quorum, no shared clock."
  6. Semantic failure. Quote: "\"The incident was contained\" in the agent's summary is a claim, not a fact."
- Closing: "None of these break the thesis. They sharpen it." Retry ceiling `size(context.attempts) < 3` "bounds the first two, but nothing in the machine bounds the third unless the escalation queue itself has a capacity plan: a depth limit, a paging threshold, and an owner."
- Confidence: High

## A11. Scale reasoning at 10 / 100 / 10,000 (file 26)
- Layer: Big Picture
- Source: file 26, "What changes at 10, 100, 10,000". Quote: "The through-line is that the architecture you need at 10,000 is the same architecture you need at 10. The difference is that at 10, you can get away without it."
- Evidence:
  - 10: "you can babysit... The system is reliable because you are the reliability layer. That is fine for a prototype and a demo. It is not an architecture."
  - 100: "you stop being the reliability layer... The journal, the idempotency keys, and the escalation queue stop being nice-to-have and start being load-bearing."
  - 10,000: "You need admission control, rate limiting, and chaos testing. A single bad state transition fans out across thousands of runs, so a bug is not an incident, it is a population." Also needs replay, observability, "a control plane to roll back a bad version of the workflow".
- Confidence: High

## A12. Worked cost formula (file 26)
- Layer: Implementation
- Source: file 26. Quote: "The formula is concurrent workflows x average tokens per workflow x cost per token"
- Evidence (assumptions stated by author): workflow averages "40,000 tokens end to end" at "$10 per million tokens" = "$0.40 per workflow". Results as stated: at 10 "a few dollars"; at 100 "$40 per 100 workflows on a good day, and a retry storm that doubles the attempt count doubles the line item"; at 10,000 "$4,000 on a good day". Character of the number per order of magnitude: "a rounding error, a dashboard tile, a budget line." Quote: "The architecture does not change. The way you watch the number does."
- Note: figures are the author's illustrative assumptions, not measurements.
- Confidence: High (explicit); illustrative, not empirical

## A13. Operating in production (file 26): versioning, savepoint rollout, escalation queue owner
- Layer: Implementation
- Source: file 26, "Operating this in production".
- Evidence:
  - Versioning. Quote: "Version the FSM the way you version a service." and "runs that started under version 1 keep running under version 1's rules while new runs start under version 2."
  - Savepoint rollout. Quote: "take a checkpoint of what is in flight, ship the new version, and let the in-flight runs finish under the old one while new runs start under the new one." Failure mode: "That is not a reliability failure. It is an audit failure".
  - Escalation owner. Quote: "A dead letter queue without an owner is a pile." Needs "a named owner, a paging threshold (a queue depth that moves the on-call, not a failure count that moves the on-call), and a working definition of \"stuck\" that is a query against the store, not a feeling."
- Confidence: High

## A14. Mapping table: stream processing (Kafka/Flink) -> agent analogue (file 27)
- Layer: Big Picture / Modeling
- Source: file 27. Quote: "a stream processor is the closest thing to an agent runtime that we already know how to make reliable."
- Evidence (table transcribed, "Stream processing | Agent system analogue"):
  - Kafka topic (append-only, partitioned) | The event journal: every trigger, context write, tool call, and effect, in order, per workflow
  - Producer | Whatever injects events: alerting and monitoring systems, webhooks, timers, other agents
  - Consumer group | The runtime process driving a workflow. One workflow has one driving consumer at a time
  - Offset | The journal position. Where a workflow is, exactly
  - Keyed state | Workflow context, keyed by workflow id. The typed fields the FSM reads and writes
  - Operator | One step of the workflow: a state, an agent turn, a tool call
  - Operator chain | The FSM's declared path through its states
  - Watermark / event time | The workflow's own clock: what has happened so far, independent of when the process last looked
  - Checkpoint | A durable snapshot of (state, context, offset). What a restart restores
  - Barrier alignment | Ordering a checkpoint around in-flight effects: nothing in flight is lost or doubled
  - Exactly-once processing | The goal. What idempotent effects plus the journal give you (Part 1's pattern)
  - Replay from an offset | Reconstructing a run from its journal: debugging, forking, recovery
  - Backpressure | Throttling agent concurrency to what models, APIs, and humans can absorb
  - Dead letter topic | The escalation queue: a workflow handed to a human because containment or verification failed
  - Savepoint | A deliberate, user-triggered snapshot: upgrade the workflow definition without losing in-flight runs. This is also how you roll a new model or prompt version: take a savepoint, swap the definition, let in-flight runs finish under the old one
  - Side output | The side channel for events that do not fit the main path: rejected alerts, uncertain hypotheses, audit records
  - Lag monitoring | The gap between what happened and what the runtime has processed: your queue depth, your stuck-workflow count
  - Schema evolution | Evolving the workflow's context and state machine while runs are in flight. The same discipline covers a prompt or model change: the context schema is the contract, and a breaking change is a deploy, not an edit
  - Consumer lag | The distance between the journal head and the offset a workflow has processed. The metric you page on, the way a data platform pages on consumer lag
  - Canary / blue-green deploy | Canarying a new model or prompt version on a slice of traffic before it goes to 100 percent, with a rollback path that is a savepoint, not a prayer
  - Capacity plan | Sizing the worker pool against p99 tool-call latency and model throughput, not against the average
- Confidence: High

## A15. Journal / replay / checkpoint: "Recovery is replay, not restart" (file 27)
- Layer: Modeling / Implementation
- Source: file 27. Quote: "Recovery is replay, not restart. After a failure, you restore the last good checkpoint and replay the events since it."
- Evidence: Motivating failure: restart at 8:48 a.m.; "the workflow's \"state\" was a growing prompt in process memory"; "31 incidents are gone"; re-run from top caused a repeat rollback and second page. Three properties: "The journal is the source of truth, not the process."; state keyed and durable; "After a crash, you do not ask the agent \"where were you?\" You restore the last snapshot, replay the journal since it". Mechanism: containment "was journaled with a key before it fired, and the replay sees the key and skips the effect." Python replay shape: `apply` and `route` are pure: "The apply and route steps are pure." Quote: "The model's next turn is the only non-deterministic step, and it happens after the replay, at the head of the stream, where it is contained." FSM policy field: `checkpointEvery: 2`.
- Confidence: High

## A16. Checkpoint + idempotency key are both needed (file 27)
- Layer: Modeling
- Source: file 27. Quote: "The checkpoint gives you the ordering. The key gives you the safety. You need both."
- Evidence: reason: "You cannot rewind an external API." Effect must be journaled "with a key before you fire it, and make the downstream system deduplicate."
- Confidence: High

## A17. Where the analogy breaks down - stream processing (file 27) [limitations, 6 items]
- Layer: Big Picture
- Source: file 27, section "Where the analogy breaks down".
- Evidence:
  1. Probabilistic operator. Quote: "The machine is a function of its input and its state. The model is a function of its input and its temperature." "Deterministic processing" "means the orchestration is deterministic: the journal, the state, the guards, the routing, the replay."
  2. Checkpointing with external side effects. Quote: "An agent's \"in-flight data\" is often a side effect that has already left the building: a page sent, a rollback issued, an incident closed."
  3. Watermarks/event time vs human wait. Quote: "So you drop the watermark and you keep the offset."
  4. Semantic failure. Quote: "A semantic failure does not trip a circuit breaker. It does not fail a guard, unless you wrote a guard for it."
  5. Backpressure is cost. Quote: "In an agent system, backpressure protects your invoice." (Author notes "Part 7 of this series is entirely about it" - content not in the files.)
  6. Provider rate limits. Quote: "A Flink operator does not get a 429 from its input stream. A model call does". Capacity plan must cover "the provider's tokens-per-minute ceiling, the burst behavior of your own traffic... and the fallback path"; fallback is "the same pattern as a multi-region failover".
- Confidence: High

## A18. Scale reasoning at 10 / 100 / 10,000 + consumer-lag formula (file 27)
- Layer: Big Picture / Implementation
- Source: file 27. Quote: "The formula is events appended per minute - events processed per minute"
- Evidence: Assumptions stated: "roughly 10 events per incident" and runtime processing "at a steady 10 per minute on a good day". At 10: "a few events deep, and it drains before anyone notices." At 100: "a dashboard tile with a paging threshold: if the backlog for incident-triage workflows grows for 15 minutes and does not drain, page." At 10,000: "the primary health signal for the whole fleet". Character: "a hunch, a threshold, a paging rule". Per-level text: at 100 "The offset is the answer."; at 10,000 "The savepoint is how you upgrade the workflow definition without losing the 10,000 runs that are in flight."
- Note: numbers are author's illustrative assumptions. The 15-minute paging threshold is an example, not a measured value.
- Confidence: High

## A19. Operating in production (file 27): savepoint-style upgrade, lag as health signal, model swap = deploy
- Layer: Implementation
- Source: file 27, "Operating this in production".
- Evidence:
  - Savepoint-style upgrade. Quote: "Deploy a new FSM or prompt version as a savepoint-style upgrade."; "take a savepoint of what is in flight, ship the new version, let the in-flight runs finish under the old one while new runs start under the new one."
  - Lag as primary signal. Quote: "Page on the backlog trend, not on the failure count. The failure count moves after the backlog, and by then the queue is already deep."
  - Model swap. Quote: "Treat a model swap like a deploy, not a config change." Needs "a canary on a slice of new runs, an eval gate before the swap goes to 100 percent, and a rollback path that is a savepoint, not a prayer."
- Confidence: High

## A20. Failure-to-pattern table (file 27)
- Layer: Big Picture
- Source: file 27 ("The whole thing, one idea"), table "Failure | Systems name | Pattern that fixes it":
  - A run dies on a process restart | State in the operator's memory | Durable, keyed state (workflow context in a store)
  - The agent does not know where it is | No offset | The journal is the source of truth; the offset is where the run is
  - An escalated incident loses its record on restart | No durable record | The store keeps escalated runs as records, not threads; the handoff to a human survives a restart
  - A retry re-sends a page | Duplicate delivery | Idempotency key on every effect, journaled before it fires
  - You cannot say what a run did | No replay | Replay from the journal: reconstruct, debug, fork
  - Agents generate more work than they can handle | No backpressure | Admission control and throttling, sized to models, APIs, and humans
- Confidence: High

## A21. Incident-triage FSM v1 (canonical YAML, file 27) - structural facts
- Layer: Implementation
- Source: file 27 (YAML; reused by files 28, 29 per their omission notes). File 26 holds a different, later variant (adds `derive-key`, `notifyKey`, `attempts`, `notifyOutcome`; notify is a re-entrant state).
- Evidence: states assess, investigate, contain, verify, notify, done, escalate; `canTransition: [assess, investigate, contain]` for agent `ops`; `verify` loop guard `size(context.checks) < 3`; verify -> notify only on `recovered`; file 27 says of contain: "The model chooses the value; the machine owns the effect." Guards use `backend: cel`. Policy: `maxExchangesPerStage: 20`, `maxExchangesPerAgentPerStage: 10`, `checkpointEvery: 2`.
- Variant difference to note for later verification: file 27 `notify` requires `notifyRef` `from: { tool: opsTool }` with "Journaled with a key so a replay does not notify twice"; file 26 `notify` requires `notifyOutcome` and optional `notifyRef`, with an explicit `derive-key` state.
- Confidence: High

## A22. Mapping table: deterministic shell layers (file 28)
- Layer: Modeling
- Source: file 28. Quote: "the LLM should not own control flow. Put nondeterministic reasoning inside deterministic orchestration, policy, and state-management boundaries."
- Evidence (table transcribed, "Deterministic shell layer | What it decides (and what the model is reduced to)"):
  - Control flow (state machine / reducer) | The current state and the legal next state. Transitions are declared edges; the model contributes a value that a guard routes on, never a next step.
  - Policy (what is allowed) | Which tools, states, and agents are in scope, and the budgets (max exchanges, max attempts). The model works inside the allowlist; it does not choose scope. This is also where model routing and fallback live: which model handles this state, and which backup model takes over when the primary is rate-limited, degraded, or down.
  - State (what is true) | The durable, typed context. Single source of truth, journaled, re-hydrated every turn. The model reads and contributes to it; it holds no private copy.
  - Guardrails (what the model may not do) | The hard limits: undeclared edges cannot fire, undeclared tools cannot be called, out-of-type values are rejected. A guard that fails simply does not fire. A guard-rejection rate that climbs is also a monitoring signal: it usually means a model or prompt change shipped that the eval suite did not catch.
  - Tool boundaries (what the model may call) | A fixed, typed interface. The model sees only the tools the current state exposes, and calls only those.
- Confidence: High

## A23. Core principle and the motivating failure (file 28)
- Layer: Big Picture
- Source: file 28. Quote: "The model was the payload, and it was also the scheduler. It decided what to do next, and it decided when it was done."
- Evidence: Agent declared "Rollback applied. Incident resolved." and "never ran the verification step"; p99 "still climbing" at 9 a.m. Lineage: "It is the Redux and xstate lineage: a component dispatches an action (it contributes data), and a reducer decides the new state (it owns the control flow)." Three properties: "Control flow is declared, not decided."; "Legality is structural, not evaluated at fire time."; "State is external and the model is re-hydrated." Quote: "done is a terminal state the machine reaches, not a verdict the model contributes."
- Confidence: High

## A24. Reducer loop shape (files 28, 29)
- Layer: Implementation
- Source: file 28 (`advance`) and file 29 (`resume`, adds `store.load`/`store.save`). Quote (file 28): "The loop parks when it is waiting for a contribution, and it fires the first eligible edge when it is not."
- Evidence: Three steps in code comments: "1. Eligibility: are all required context fields present?" "2. Routing: the first declared edge whose guards all pass fires." "3. Fire: run the onTransition actions (literal writes only), then move." Uses `MICROSTEP_CAP`. File 29 adds "every move is journaled". Binding detail deferred to another post: "I covered the implementation of this in depth in Binding LLMs to Finite State Machines Using the Reactive Reducer Pattern." (file 28; cross-link, that post is in the original 18).
- Confidence: High

## A25. Where the analogy breaks down - deterministic shell (file 28) [limitations, 6 items]
- Layer: Big Picture
- Source: file 28, section "Where the analogy breaks down".
- Evidence:
  1. Guard coverage. Quote: "The shell is only as good as the guards you wrote. A guard you did not think of is a hole."
  2. Semantic failure inside the shell. Quote: "A deterministic shell around a wrong answer is still a wrong answer, just a well-organized one."
  3. Rigidity. Quote: "If the shell is so rigid that every surprise is an escalation, you have not built an agent. You have built a form with a language model filling in the blanks"
  4. Boundary is a design choice. Quote: "Draw that line too far toward the machine and the model is a token-expensive lookup. Draw it too far toward the model and you are back to the 8:50 failure."
  5. Safe, not smart. Quote: "The shell does not make the model smart. It makes the model safe."
  6. Shell is a deployable artifact. Quote: "A change to a guard is a change to production behavior". Without a release process "It is just deterministically wrong, for everyone, starting the moment you merge." Also notes the shell "needs a release process" incl. "an eval suite it has to pass before it merges, a staged rollout".
- Confidence: High

## A26. Scale reasoning at 10 / 100 / 10,000 + guard-suite/canary reasoning (file 28)
- Layer: Big Picture / Implementation
- Source: file 28. Quote: "At 10,000 concurrent workflows, the shell is the only thing between you and a population of wrong control-flow decisions."
- Evidence: At 100: "a model that re-plans its own control flow on every turn is a model that burns tokens deciding things the machine should have decided for free." Worked numbers (author-stated, qualitative): "A guard suite of 30 assertions takes a few seconds to run at 10 workflows"; at 100 "it runs in CI, on every change, and it blocks the merge if it fails"; at 10,000 "you also want a canary, a fixed slice of traffic (1 percent, or a single low-stakes workflow type) that gets the new shell first, for an hour". Quote: "The cost of skipping the canary does not change with scale. What changes is how many runs are wrong before you notice."
- Confidence: High

## A27. Operating in production (file 28): PR-reviewed shell, model routing + fallback, guard-rejection rate
- Layer: Implementation
- Source: file 28, "Operating this in production".
- Evidence:
  - Ships like code. Quote: "The shell ships like code, because it is code, even when it is YAML." Needs "a pull request, a reviewer who is not the author, and an eval suite that has to pass." Eval suite catches a guard "too loose" or "too tight".
  - Model routing + fallback. Quote: "Model routing lives in the policy layer, and it needs a fallback, not just a primary."
  - Guard-rejection rate. Quote: "Guard-rejection rate is the metric to put on a dashboard, next to the business metrics." Spike means "a model or prompt change shipped that the eval suite did not catch, or a real shift in the kind of input arriving". "it is a signal worth paging on".
- Confidence: High

## A28. Failure-to-pattern table (file 28)
- Layer: Big Picture
- Source: file 28, table "Failure | Systems name | Pattern that fixes it":
  - The model decides the workflow is over | Control flow in the payload | The machine owns the terminal state; done is a declared state, not a model decision
  - The model skips a required step | Undeclared behavior | The step is a required state; the edge to done is guarded by it
  - The model re-orders the steps | Emergent control flow | Transitions are declared edges; the model contributes values, not next steps
  - The model calls a tool it should not | No tool boundary | Tool boundaries: a fixed, typed interface scoped by policy
  - The model holds a private copy of state | State in the model's head | The state store is the single source of truth; the model is re-hydrated every turn
  - A guard you did not write | Missing guard | The shell is only as good as its guards; author them deliberately
- Confidence: High

## A29. Mapping table: task list -> FSM (file 29)
- Layer: Modeling
- Source: file 29. Quote: "a task list describes intentions, and a state machine describes legal system behavior. Reliable agents need explicit state, guards, and transitions, not a list of steps."
- Evidence (table transcribed, "Task list (intentions) | FSM (legal behavior)"):
  - A step in the list ("verify the metric") | A state with an objective (verify: re-read the p99 and confirm recovery)
  - "Then do the next thing" | A declared transition (an edge from verify to notify)
  - "Done when the metric recovered" | A guard (verifyOutcome == recovered) on the edge out of verify
  - "If it fails, try again" | A bounded re-entry edge (verify to verify, guarded by size(checks) < 3)
  - "Where am I after a crash?" | Durable state: (state, context) journaled and re-hydrated on restart
  - "Is this step allowed now?" | Legality is structural: an edge not in the YAML cannot fire
  - "When is the workflow over?" | A terminal state the machine reaches, not a verdict the model writes
- Also: "A task list is a plan. A plan is not a position." The task list is "demoted from the control flow to the description of the states."
- Confidence: High

## A30. Operability table: task list vs FSM (file 29)
- Layer: Implementation
- Source: file 29. Quote: "A state is a place. It has a name, an entry count, and a duration."
- Evidence (table, "What you want to know | Task list | FSM"):
  - How many runs are stuck right now, and where | No answer: a list has no position to query | Backlog per state: count(*) where state = 'verify'
  - Is this step usually fast or slow | No answer: steps are not named, durations are not tracked | Time-in-state: a distribution per state, with a p99 you can alert on
  - Where should more workers go | No answer: the whole agent is one undifferentiated loop | Worker pool sizing per state: cheap states (assess) need few workers, model-heavy states (verify) need more
  - Did the new version make things worse | No answer: nothing to diff against | Compare backlog and time-in-state, state by state, before and after a deploy
- Quote: "You cannot build a capacity plan on top of \"the agent is somewhere in a six-step list.\""
- Confidence: High

## A31. Motivating failure: task list has no position (file 29)
- Layer: Big Picture
- Source: file 29. Quote: "a list of intentions is not a position. It has no guards, no concept of an illegal state, no re-entry, and no durable place to resume after a crash."
- Evidence: 8:48 restart; re-assessed, re-investigated, rolled the deploy back again; "never verified the metric actually recovered." Structural fix claims: "there is no edge from contain to done that bypasses verify."; "The double-contain is structurally impossible."
- Confidence: High

## A32. Where the analogy breaks down - task list vs FSM (file 29) [limitations, 6 items]
- Layer: Big Picture / Modeling
- Source: file 29, section "Where the analogy breaks down".
- Evidence:
  1. Not every workflow needs an FSM. Quote: "Do not model a three-step linear script as a state machine. You will have built a machine to do what a for-loop could do for free."
  2. FSMs are authored, not learned. Quote: "If the workflow is open-ended... it does not fit a finite state machine, because the states are not fixed."
  3. Guards only as good as written. Quote: "If you forgot to guard the metric recovery on the verify, the machine will declare the incident done with the service still degraded and never blink."
  4. Task list still useful. Quote: "The state machine is the control flow; the task list is the documentation."
  5. Over-modeling. Quote: "If you model every micro-decision as a state and every micro-condition as a guard, you have not built a machine that runs the workflow."
  6. Versioning. Quote: "changing it is a production deploy: it needs a diff someone reviews, a version number, and a way to canary the new definition".
- Confidence: High

## A33. Scale reasoning at 10 / 100 / 10,000 + per-state bottleneck formula (file 29)
- Layer: Implementation
- Source: file 29. Quote: "Say verify takes a median of 8 seconds (a metric re-read plus a model turn to judge recovery) and every other state takes under a second (tool calls)."
- Evidence: At 10: "that imbalance is invisible". At 100: "verify is the bottleneck: if your worker pool assigns workers uniformly across states instead of weighting toward the slow one, verify's backlog grows while assess's workers sit idle". Diagnostic named: "count(*) where state = 'verify' against count(*) where state = 'assess'". At 10,000: "an unbounded queue". Quote: "The cost of the imbalance does not change with scale. What changes is whether the backlog empties before the next spike, or compounds into one."
- Note: no numeric lag formula here; the 8-second median is the author's assumption.
- Confidence: High

## A34. Operating in production (file 29): backlog per state, dashboard by state, version bump = deploy
- Layer: Implementation
- Source: file 29, "Operating this in production".
- Evidence:
  - Quote: "Alert on backlog per state, not on failure count."
  - Quote: "Dashboard the fleet by state, not by workflow." Example row shape: "assess: 3 running, 0 stuck, verify: 14 running, 2 stuck for > 10 min".
  - Quote: "A workflow that started under version 3 of the FSM should not be silently reinterpreted under version 4's guards mid-run".
- Confidence: High

## A35. Failure-to-pattern table (file 29)
- Layer: Big Picture
- Source: file 29, table "Failure | Systems name | Pattern that fixes it":
  - The agent re-runs an early step after a crash | No durable position | Durable state: (state, context) journaled and re-hydrated on restart
  - The agent skips a required step | Undeclared behavior | The step is a required state; the edge to done is guarded by it
  - The agent does a step twice (double-contain) | Duplicate effect | The machine owns the transition; the effect is journaled with a key
  - The agent re-plans its own position every turn | Control flow in the payload | The reducer routes on declared edges; the model contributes values, not next steps
  - The agent declares done with steps left | No terminal state | done is a terminal state the machine reaches, not a verdict the model writes
  - A re-check loop runs forever | Unbounded loop | A bounded re-entry edge guarded by a list-as-counter
- Confidence: High

## A36. Author's honest caveats / admissions across AIP files
- Layer: Big Picture
- Source and quotes:
  - file 26: "The bad news is that the node is probabilistic, stateless, expensive, and occasionally wrong in ways a process never is."
  - file 27: "The honest caveat comes at the end of this article, because the operator on the right side is not a pure function. It is a model."
  - file 28: "I want to be honest about the limits of this design, because a good principle taken too far is worse than no principle at all."
  - file 28: "None of these break the thesis. They sharpen it." (same sentence in 26, 27, 29)
  - file 28: "Those need guards you write deliberately, and they need a human in the loop when the stakes are high."
  - Pattern note: each file's "10/100/10,000" figures are labelled "what I have actually seen" but contain no data or named systems; quote (file 26): "Here is what I have actually seen."
- Confidence: High (that these statements exist); no external verification of the author's claimed experience

## A37. Series cross-links and forward references found in AIP files
- Layer: Big Picture
- Source: files 26-29 closing lines. Quotes: file 26 "Up next: Part 2 (What Kafka and Flink Taught Me)"; file 27 "Up next: Part 3 (Deterministic Shell, Probabilistic Core)"; file 28 "Up next: Part 4 (Stop Treating Agent Workflows Like Task Lists)"; file 29 "Up next: Part 5 (Exactly-Once Effects)".
- Evidence: file 27 refers to "Part 7 of this series is entirely about" backpressure. Parts 5-7 are not in the local files (gap).
- Confidence: High

---

# PART B. Self-evolving agents series (files 22-25)

## B1. Loop structure common to all four examples (files 22-25; summary in file 25)
- Layer: Big Picture / Modeling
- Source: file 25. Quote: "The agent starts by doing the work by hand and failing in a way the clean journal hides. The eval names the gap with a number, not an adjective."
- Evidence (continuation in file 25): "The agent builds the deterministic tool for the half of the job that is computation. It builds the LLM lane for the half that is judgment. It builds the gate that makes the workflow able to fail. And it tunes, one change at a time, until its own measurements stop moving."
- Round structure (each file): Round 0 baseline broken; Round 1 measurements; Round 2 tool; Round 3 FSM with gate; Round 4 tuning; optimum table.
- Human's role. Quote (file 24): "The human's part is the task, the yardstick, and the approval of the dangerous operations. Everything else is the agent's."
- Confidence: High

## B2. Round 0 baseline and "a clean journal is not a good outcome" (files 22, 23, 24, 25)
- Layer: Big Picture
- Source: file 22. Quote: "A clean journal is not a good outcome. The journal records how the process ran. It says nothing about whether the output is right."
- Evidence: file 22: "A workflow with no quality gate cannot fail, so the journal stays clean while the output breaks silently." file 23: "The journal proved the process ran smoothly. It proved nothing about whether the output was right." file 23 closing: "The cleanest journal in the world is worthless if nothing in it can fail." file 24: "A workflow that cannot fail cannot tell you when it has failed, so the agent that builds its own workflow has to build the gate too." file 25: "The journal proved nothing about whether the process was right."
- Confidence: High

## B3. Three signals (efficiency / completeness / quality) and facts-before-opinion (files 22, 23)
- Layer: Modeling
- Source: file 22 table "Signal | What it reveals | Tool":
  - Efficiency | How did the process run? | getFsmRun
  - Completeness | Is the output actually complete? | Deterministic recompute check
  - Quality | Was the output any good? | evalById (LLM critic)
- Quote (file 22): "Three signals. Numbers before opinions."
- Evidence (file 23, table "Signal | Question | Kind"): Run journal (getFsmRun) | Fact; Completeness check (deterministic) | Fact; Claim check (deterministic recompute) | Fact; Critic (evalById) | Opinion. The file notes a counting inconsistency: its text says "Two facts, one opinion" while the table lists three facts and one opinion (flagged "[sic ...]" in the file). Quote (file 23): "The opinion is the weakest of the three, and the agent is told to state the numbers before it interprets them."
- Quote (file 23): "The critic is a hint."
- Confidence: High

## B4. Tool + LLM + gate split (files 22, 23, 24, 25)
- Layer: Modeling
- Source: file 22. Quote: "The tool crushes the volume. The LLM interprets. A gate between them checks that the interpretation is correct."
- Evidence: file 23: "The tool owns the volume, the LLM owns the judgment, and the gate owns the boundary between them." file 25: "a deterministic tool in its lane, an LLM in its lane, and a gate that checks the boundary between them." Gate is deterministic: file 22 "It is a checkable fact, not an opinion." file 23 "verify does not ask the LLM whether the report is good. It recomputes the profile from the raw data and diffs the report's claims against the fresh numbers."
- Why a tool for the deterministic half but not the variable half (file 25): "For the deterministic half, you should write the tool. For the variable half, a tool would fail on the new cases. The LLM's generalization is the feature, not the bug."
- Confidence: High

## B5. Order reversal: LLM first, then tool (file 24)
- Layer: Modeling
- Source: file 24. Quote: "free text cannot be aggregated until it has been understood. The LLM has to classify and extract first. Only then does the deterministic tool have something to aggregate."
- Evidence: Parts 1 and 2 had tool first then LLM. File 24 links this to the AIP series: "It is the deterministic shell, probabilistic core split from the Agents in Production series, except here the shell is something the agent builds and tunes itself." Schema is the boundary: "The boundary between them is the schema." Enum fields (issue_type: billing | refund | bug | feature_request | account | other; severity: low | medium | high) force consistent labels: "The enum is the constraint".
- Confidence: High

## B6. FSM DSL rules called out in the self-evolving posts (file 22; reinforced 23, 24, 25)
- Layer: Implementation
- Source: file 22, bullet list after the YAML. Quotes:
  - Typed context: "Every field is declared with a type. There is no default: key anywhere."
  - Literal-only assign: "The assign writes literals only... It cannot increment a counter or copy one field into another."
  - Loop-bounding via list: "The counter is the data. The guard size(context.rejections) < 3 bounds the loop"
  - Consume-the-trigger: "Blanking the trigger forces a fresh draft on every re-entry."
  - Re-entrant trap: "A re-entrant state whose requirement is already set will not re-collect. So the guard that decides the loop must compare (size(context.rejections) < 3), not test presence."
  - canTransition: "Omit one and the run stalls permanently, because the agent is not allowed to act in a state it is not listed in."
- Confidence: High

## B7. Example 1: log triage (file 22) - FSM and metrics
- Layer: Implementation
- Source: file 22. FSM `log-triage` version 1: states triage -> root_cause -> verify -> {report -> done | root_cause (size(rejections) < 3) | escalate (>= 3)}; policy `maxIterations: 8`, `maxExchangesPerStage: 10`. Quote: "escalate is the ceiling. If the gate rejects three times, the run stops and reports the ceiling instead of looping."
- Failure: "At line 300,000 there is an error class that appears 41 times. SignatureVerificationFailed from the webhook receiver." The baseline agent "compressed it into \"a handful of webhook errors,\"". Quote: "Hand-reading a 40MB file is a lossy compression algorithm."
- Tool: `log_triage` does parse and normalize; dedupe by error signature; aggregate; summarize ("a few kilobytes, not forty megabytes").
- Baseline-vs-optimum table (as stated in file 22):

| Metric | Round 0 (baseline) | Optimum |
|---|---|---|
| Tokens | 612,400 | 26,400 |
| Completeness | 60 percent | 100 percent |
| Re-entries | 0 (no gate) | 1 (gate caught a real gap) |
| Critic score | 8.6 | 9.3 |

- Intermediate numbers as stated: Round 2 tool-only: tokens 24,800, completeness 100 percent, critic 6.1 ("Accurate counts. But it is a list, not an explanation."). Round 3: tokens 26,400, completeness 100 percent, re-entries 1, critic 8.9. Round 0 completeness detail: "The recompute check found 20 distinct error classes in the file. The report only named 12."
- Tuning (Round 4): change one, signature count "went from 18 to 20"; change two, critic "went from 8.9 to 9.3" via severity flag; change three, "Nothing" (reject-loop bound stays 3: "the measurements do not show a problem, so the agent does not build a solution").
- Convergence: Quote: "Three consecutive runs with no improvement." Signal: "the token count... 26,400, 26,400, 26,400."
- Note: the critic score ROSE in this example (8.6 -> 9.3), unlike file 23 (8 -> 8) and file 24 ("thorough" -> "thorough").
- Confidence: High

## B8. Example 2: data quality audit (file 23) - FSM and metrics
- Layer: Implementation
- Source: file 23. Task: "Two million rows, 48 columns, about 1.8 gigabytes of CSV." Failure: "at row 1.2M, the category column drifted. Eight categories became fourteen."
- FSM `data-quality-audit` version 1: profile -> classify -> verify -> {done | classify (verdict != approved && size(verdicts) < 3, `assign report ""`) | escalate (>= 3)}; verify requirement from `{ tool: audit_verify }`; policy `maxExchangesPerStage: 20`, `maxExchangesPerAgentPerStage: 10`, `checkpointEvery: 2`. Escalation: "A human takes over with the full history." Quote: "When the budget is spent, the machine does not guess. It hands off to a human with the full history."
- Gate caught real error: "The report claimed 12 percent nulls in customer_id. The recompute said 3.1 percent."
- Baseline-vs-optimum table (as stated in file 23):

| Metric | Round 0 (baseline) | Optimum |
|---|---|---|
| Tokens | 412,000 | 41,000 |
| Columns profiled | 11 of 48 | 48 of 48 |
| Unverified claims | 14 of 14 | 0 |
| Critic score | 8/10 | 8/10 |

- Other stated numbers: Round 2 tool-only tokens "38,000"; "26.5 percent of the columns were profiled"; Round 4 change one "classify tokens dropped from 21,000 to 9,000"; change two "false-positive rejections stopped". Note: Round 2 reports 38,000 tokens while the optimum table reports 41,000 (both stated in the file; not reconciled in the text).
- Convergence signal: Quote: "the gate stopped rejecting. The gate is the only thing that can fail. When it stops failing, the workflow has converged." (three consecutive runs). Quote on critic: "The optimum is not a higher critic score. The optimum is zero unverified claims, every column profiled, and a gate that can reject and did not."
- Confidence: High

## B9. Real-world artifact claim: disease-etl five versions (file 23)
- Layer: Implementation
- Source: file 23, "This really happened". Quote: "It went through five committed versions, and each one is archived."
- Evidence: v1 gate "reading the file on disk. It was checking the wrong thing"; v2 verify "the records themselves"; v3 added `compact=true` flag to fetch tool "because the raw payload was burning tokens"; v4 pinned the target year into context; v5 "told the worker to copy the raw string verbatim, because the model was re-serializing the nested object by hand and mangling the structure." Archive path claimed: ".aegis/fsms/.history/disease-etl.1.yaml through disease-etl.4.yaml, with disease-etl.yaml at version 5." Quote: "The archive is where the learning lives: not in the final YAML, but in the diffs between the versions."
- Caveat from author: "That is not my data-quality scenario. It is a separate task that ran in the same harness". The archived files are not available locally (unverified).
- Confidence: Medium (explicit claim, but not independently inspectable; the author's own scenario in 22-25 is presented as illustrative)

## B10. Disciplines of the tuning loop (file 23)
- Layer: Modeling / Implementation
- Source: file 23. Quotes (four disciplines):
  - "One change per round. If it batches, split it."
  - "Check before you build. listFsms and listDynamicTools before authoring anything new."
  - "Report the regression. A loop that only ever reports improvement is broken."
  - "Report the ceiling. When three rounds show no measurable improvement, stop and say so."
- Confidence: High

## B11. Example 3: support ticket mining (file 24) - FSM and metrics
- Layer: Implementation
- Source: file 24. Task: "5,000 free-text support tickets." Round 0 failure: "By ticket 3,000, the agent has lost the running tally. It re-tallies from scratch. The new tally does not match the old one." Quote: "The LLM is doing two jobs at once: understanding text and keeping the books. It is bad at keeping the books."
- Round 1 stated measures: tokens 1.9M; re-entries 214; completeness 4,612 of 5,000 ("388 tickets that were never classified"); consistency "96 pairs of the same issue type classified two different ways"; critic "thorough and well-organized".
- FSM `ticket-mining` version 4: extract -> verify -> {extract (rejected && size(passes) < 10, appends "pass", blanks batch and verdict) | report (approved) | escalate (>= 10)}; verify from `{ tool: ticket_verify }`; report from `{ tool: ticket_agg }`; `canTransition: [extract]`. Gate described: "It runs ticket_verify, a deterministic tool that recomputes the expected output from the raw input and diffs it."
- Baseline-vs-optimum table (as stated in file 24):

| Metric | Round 0 (baseline) | Optimum |
|---|---|---|
| Tokens | 1.9M | 410K |
| Re-entries | 214 | 3 |
| Tickets processed | 4,612 of 5,000 | 5,000 of 5,000 |
| Inconsistent pairs | 96 | 0 |
| Critic score | "thorough" | "thorough" |

- Tuning stated: batch size 500 per pass ("Ten passes, not 5,000"), "tokens drop from 1.9M to 410K. Re-entries drop from 214 to 9"; summary field trimmed 140 -> 40 chars plus a confidence enum, "re-entries drop from 9 to 3", "0 pairs classified two different ways"; verify tightened to "exactly one record" per ticket, completeness "jumps to 5,000". Derived statements by author: "Tokens down 78 percent. Re-entries down 98 percent."
- Convergence: Quote: "The inconsistent pairs stayed at zero for two consecutive runs." (also: "The agent runs three more times. The metrics stay flat.")
- Confidence: High

## B12. Example 4: entity resolution and data fusion (file 25) - FSM and metrics
- Layer: Implementation
- Source: file 25. Task: "Three overlapping record sets... Roughly 12,000 records". Yardstick: "a deterministic check that recomputes the expected grouping from the raw input and diffs it". Quote: "A false merge or a false split is a checkable fact."
- Tool `entity_block`: normalization, exact-key dedupe, blocking ("normalized surname plus a coarse location token"), joins.
- FSM `entity-resolve` version 4: block -> resolve -> verify -> {done (approved) | resolve (size(attempts) < 3; appends "rejected"; blanks resolution and verdict) | escalated (>= 3)}; context includes `blocking_key`, `threshold` (type decimal), `attempts`. Quote: "The guards are all CEL, in the same form the published Agents in Production series uses. One form, used consistently."
- Round 1 stated measures: tokens 41,200; false merges 2; false splits 3; unmatched 174; completeness "11,826 of 12,000"; critic "thorough and well-reasoned".
- Baseline-vs-optimum table (as stated in file 25):

| Metric | Round 0 (baseline) | Optimum |
|---|---|---|
| Tokens | 41,200 | 6,400 |
| False merges | 2 | 0 |
| False splits | 3 | 0 |
| Unmatched records | 174 | 0 |
| Reproducible | No | Yes |

- Tuning stated: blocking key tightened, "candidate pairs per block dropped from a median of 41 to 9"; similarity threshold 0.8 -> 0.7 ("false splits dropped from 3 to 1"), then 0.7 -> 0.65 ("false splits dropped to zero. False merges stayed at zero.").
- Convergence: Quote: "the false-merge and false-split count hitting zero and staying at zero across two consecutive runs." Quote: "That is the sharpest signal in the whole series."
- Internal inconsistency in source text (not resolved): the Round 3 FSM prose says "if verify rejects three times" and tuning prose lists "Change three: the threshold again", while the "builds because" paragraph attributes the blocking-key tuning to "Round 3" and threshold tuning to "Round 4"; round numbering in that closing paragraph does not match the headings. Also the converged FSM YAML has no `policy` block and its guard ordering uses only attempts count on the reject edge (no verdict test).
- Confidence: High (numbers); Medium (round-attribution text)

## B13. Cross-example convergence / "report the ceiling" and its restraint claim (files 22-25)
- Layer: Big Picture
- Source: file 25. Quote: "This one ran the same change a third time, saw the same number, and reported the ceiling."
- Evidence of convergence signals per example: file 22 flat token count x3; file 23 "the gate stopped rejecting" x3 runs; file 24 consistency at zero for two runs; file 25 false merge/split at zero for two runs. Quote (file 22): "The optimum is not a target the agent was told to hit. It is the point where the agent's own measurements stopped moving."
- Confidence: High

## B14. Author's honest caveats in the self-evolving series
- Layer: Big Picture
- Source and quotes:
  - file 25: "I am not sure that line between \"the human's job\" and \"the agent's job\" is where it will stay for long, and I am not sure that is a bad thing."
  - file 25: "That restraint... is the part I would not have trusted an agent with a year ago."
  - file 23: "I keep this scenario as the main thread because it is clean to explain, but I want to point at the real artifact, because the pattern is not hypothetical." (i.e. the four worked examples are presented as scenarios; only the disease-etl run is described as a distinct real artifact.)
  - file 22: "The optimum is not what I would have designed. It is what the agent discovered by iterating on its own feedback."
  - No file states sample sizes, number of repetitions, or whether the metrics were measured on a real run versus illustrative; the numbers are given without methodology. (Absence observed; no quote.)
- Confidence: High (statements exist); evidentiary strength of the metrics tables = unestablished

## B15. Human approval of dangerous operations (files 24, 25)
- Layer: Big Picture
- Source: file 24. Quote: "The human's part is the task, the yardstick, and the approval of the dangerous operations."
- Evidence: file 25 repeats: "Give the task. Give the yardstick. Approve the dangerous operations." No detail on what counts as dangerous or how approval is wired in these files.
- Confidence: High

---

## Declarative Conclusions
Not applicable (external written sources, not transcripts).

## Rejected Information

| # | Information | Source | Rejection Reason | Re-include If |
|---|---|---|---|---|
| 1 | Full YAML bodies of the six FSMs (log-triage, data-quality-audit, ticket-mining, entity-resolve, incident-triage x2 variants) copied in full | files 22-27 | Too long for a findings file; only structural facts and DSL rules recorded. Verbatim YAML remains in the raw files | A downstream step needs exact DSL syntax to compare to Aegis/FSM docs |
| 2 | Python pseudocode bodies (log_triage, verify, send_notification, replay, advance, resume) | files 22, 23, 26-29 | Implementation illustrations; essence captured in prose findings | Implementation-level pattern comparison needed |
| 3 | Narrative incident openings (8:41 a.m. checkout latency, 2:47 AM log, etc.) | files 22-29 | Storytelling; only the failure mechanics are recorded | A scenario-based explanation is needed |
| 4 | Mermaid/diagram residue descriptions | all files | Headers state diagram residue omitted; bracketed diagram descriptions are redundant with the prose | Graph topology must be verified against YAML |
| 5 | Part 5-7 content of AIP series (Exactly-Once Effects, backpressure article) | file 27, 29 (forward references only) | Not in local files; not gathered (outside this category's inputs) | Parts 5-7 are supplied as raw text |
| 6 | Self-evolving series "Intro" page | file 25 ("Back to the start: Intro") | The intro is in the original 18 URLs (self-evolving-agents-intro); handled by another category | Cross-check of series claims against the intro is required |
| 7 | Tool names specific to the author's harness (getFsmRun, evalById, listFsms, listDynamicTools, Aegis, .aegis/fsms/.history) | files 22-25 | Harness specifics; recorded only where they carry the pattern (signals table, disciplines) | Mapping onto a specific toolchain is needed |
| 8 | Non-pattern examples inside incident-triage prose (the specific rollback, p99, paging API details) | files 26-29 | Domain detail of the example, not a reusable pattern | Domain transfer analysis is needed |

## Actor Relevance
No actors were defined in the research plan ("None - research is actor-agnostic"); section omitted.
