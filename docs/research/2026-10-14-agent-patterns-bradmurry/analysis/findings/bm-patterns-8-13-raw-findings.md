# Findings: bm-patterns-8-13-raw

Scope: patterns 8-13 of Brad Murry's "13 Multi-Agent Orchestration Patterns, Implemented via the Reactive Reducer" series, gathered from full-text local files in `C:\Projekty\ECommerceApp\docs\research\2026-10-14-agent-patterns-bradmurry\analysis\sources-raw\`.

Source key (used in every `Source:` line below):
- **[05]** = `05-reactive-reducer-patterns-13.md`, URL https://bradmurry.com/software/reactive-reducer-patterns/ (index post; only its sections 8-13 and the closing idioms were read for this file).
- **[16]** = `16-pattern-08-planner-executor.md`
- **[17]** = `17-pattern-09-generator-critic-refiner.md`
- **[18]** = `18-pattern-10-auction-contract-net.md` (header: dated "Sep 10, 2026")
- **[19]** = `19-pattern-11-event-driven-pubsub.md` (header: dated "Sep 11, 2026")
- **[20]** = `20-pattern-12-quorum-voting.md` (header: dated "Sep 12, 2026")
- **[21]** = `21-pattern-13-dynamic-team-formation.md`

URL note (gap): the task said each header comment carries the original URL. Only [05] does. The headers of [16]-[21] give the article title and "URL NOT in the user's 18-URL list; text pasted by the user 2026-10-14", with no URL. I did not guess URLs. Those files are cited by file name and article title. The [05] intro also says the per-pattern posts are "NOT in the 18-URL list".

Provenance caveat from [05] header, quoted: "full article text pasted by the user on 2026-10-14 (WebFetch had returned only fragments)." Diagrams were flattened or omitted in all files. All files are the user's raw paste, not a fresh fetch.

Confidence rule used: High = the author states it explicitly in the quoted text. Single-source claims stay at most Medium until the verification phase (plan rule). Where a finding is explicit but single-source I have written "High (explicit statement)" and left cross-source verification to that phase.

Layer tags: Big Picture (BP), Modeling (M), Implementation (I).

---

# PATTERN 8: PLANNER / EXECUTOR

## F8.1 Definition
- Layer: BP
- Source: [05] section 8 (https://bradmurry.com/software/reactive-reducer-patterns/); same text in [16] "The pattern".
- Evidence: "One agent is responsible for turning an objective into a plan, sequence of steps, or task graph, while one or more executor agents perform the actual work." Also: "This separates strategic decomposition from tactical execution."
- Confidence: High (explicit definition).

## F8.2 Good for
- Layer: BP
- Source: [16] "The pattern"; same list in [05] section 8.
- Evidence: "Open-ended objectives; Coding and implementation tasks; Research workflows; Tasks whose solution path cannot be predetermined; Long-running work that benefits from re-planning"
- Confidence: High.

## F8.3 Author's framing: feedback is the interesting part
- Layer: BP
- Source: [16] "A little background".
- Evidence: "The interesting part isn't the planning. It's the feedback: execution reveals assumptions that invalidate the plan, and the system has to re-plan without losing the work it's already done." Reducer answer: "The loop is the machine; the planning is the agent."
- Confidence: High.

## F8.4 Failure points as listed by the pattern
- Layer: BP
- Source: [16] "The pattern".
- Evidence: "Bad plans cascade into bad execution; Planner over-decomposes simple work; Execution reveals assumptions that invalidate the plan; Re-planning can become an endless loop; Planner and executor can disagree about task completion"
- Confidence: High.

## F8.5 Stated reducer answers, per failure point
- Layer: M
- Source: [16] "What the reducer does to the failure points"; condensed in [05] section 8.
- Evidence (one quote per failure point):
  - Bad plans cascade: "The executor's outcome enum is the tripwire: the moment execution reveals the plan was wrong, the executor writes replan" (the cascade "is now interruptible").
  - Over-decomposition: "Over-decomposition is now visible and cheap to fix." Reason given: "The plan is a staged, inspectable field".
  - Assumptions invalidated: "The invalidation is a first-class event - a context write that fires a declared edge".
  - Endless loop: "Bounded two ways" (see F8.9).
  - Disagreement on completion: "Completion is the executor's typed claim, and the planner's disagreement is expressed as more work, not as a second opinion in a prompt."
- Confidence: High (explicit).

## F8.6 Key mechanism: plan and directive are separate typed fields
- Layer: M / I
- Source: [16] "Three things to hold onto"; FSM YAML in [16] ("plan", "directive", "result", "history", "outcome" fields; agents `planner`, `executor`).
- Evidence: "The plan and the directive are separate fields, and that separation is the pattern." The author says plan is "strategic, revised on re-plan" and directive is "tactical, what the executor actually walks".
- Confidence: High.

## F8.7 THREE CONSUMED TRIGGERS on the replan edge (special attention)
- Layer: M / I
- Source: [16] "Three things to hold onto" and the YAML comment on the replan edge; [05] section 8 ("The loop that runs and does nothing").
- Evidence:
  - "The replan edge has to consume all three triggers - directive, outcome and result - and each survivor breaks it differently" ([05]).
  - Outcome survives: "A surviving outcome = replan re-fires this very edge the instant the machine re-enters execute, bouncing it back to replan without the executor being summoned at all." ([16])
  - Result survives: "A surviving result satisfies the executor's requirement with the previous cycle's finding, so the state falls through without a turn." ([16])
  - Directive survives: "A surviving directive means the planner's new instruction is never waited for." ([16])
  - Summary: "Three fields, three different ways to get a loop that runs and does nothing." ([16])
  - The edge consumes three and keeps one: "directive, outcome and result are cleared; plan survives, because it's the roadmap the planner is about to revise." ([16])
  - [05] labels this failure as one the author added beyond the standard list: "Not on the original list, and the one that actually bites."
- Confidence: High (explicit, consistent between [05] and [16]).

## F8.8 History list with exit append (evidence kept, trigger consumed)
- Layer: M / I
- Source: [16] "Three things to hold onto" and YAML (`exit: append: history`); [05] section 8.
- Evidence: "execute's exit action appends "<directive> → <result>" to history before the edge clears anything, so the planner revises from everything that happened, not just the last thing." The author names the general rule: "a consumed scalar for the trigger, an accumulated list for the record". YAML comment: "`exit` runs after the edge is selected and before it fires, so `directive` and `result` are both still set here."
- Confidence: High.

## F8.9 Loop bounds, and an explicit correction about the no-progress guard
- Layer: M / I
- Source: [16] "What the reducer does to the failure points", paragraph "Re-planning can become an endless loop"; [05] section 8.
- Evidence:
  - "The outcome enum has exactly three values - done and abort both exit, only replan continues - and policy.maxIterations caps the total exchange count"
  - Correction/limit: "The reducer's no-progress guard is not a third backstop here: it stops oscillation within a single advance() call, which keeps the microstep loop hazard-free, but it doesn't span turns"
  - Optional harder cap: "size(context.history) is the cycle count if you want a harder in-machine cap than the policy's - a guard, not a new field."
  - YAML policy for this example: `maxIterations: 8`, comment "bounds the execute/replan loop (headless)".
- Confidence: High.

## F8.10 Author's stated trade-off: plan is text, not a task graph
- Layer: M
- Source: [16] last paragraph before "The whole thing, one idea".
- Evidence: "One honest trade-off: the plan is text in a context field. The reducer doesn't parse it into a task graph". Also: "collections ... are one level deep by design, so a step with dependencies and per-step state isn't a context type." Author's two alternatives: encode structure inside the field content, or "reach for the hierarchical pattern, where each step is a child FSM with its own store".
- Confidence: High.

## F8.11 Author's self-correction on the scalar version
- Layer: M
- Source: [16] "Three things to hold onto", third bullet.
- Evidence: "A scalar version of this article kept only the most recent result, which is exactly the memory a planner shouldn't be working from on cycle four."
- Confidence: High.

---

# PATTERN 9: GENERATOR / CRITIC / REFINER

## F9.1 Definition and asymmetric roles
- Layer: BP
- Source: [05] section 9; [17] "The pattern".
- Evidence: "One agent produces an artifact or proposed answer, another explicitly evaluates it against criteria, and the original agent or a separate refiner improves it using the critique." And: "Roles are intentionally asymmetric: one produces, one evaluates, and one improves."
- Confidence: High.

## F9.2 Good for
- Layer: BP
- Source: [17] "The pattern".
- Evidence: "Code generation; Writing and document creation; Architecture reviews; Compliance checks; Structured-output quality; Tasks with explicit acceptance criteria"
- Confidence: High.

## F9.3 Author's framing: why the critic is separate and what the reducer changes
- Layer: BP
- Source: [17] "A little background".
- Evidence: "a generator that grades its own homework tends to grade it generously." Reducer answer: "The reactive reducer makes the gate a typed decision with a declared continuation, and the loop is bounded by construction."
- Confidence: High.

## F9.4 Failure points as listed
- Layer: BP
- Source: [17] "The pattern".
- Evidence: "Critic invents problems that are not material; Refinement loops can continue indefinitely; Quality can plateau despite additional iterations; Generator may optimize for the critic rather than the real objective; Weak criteria produce weak criticism"
- Confidence: High.

## F9.5 Key mechanism: no refine state; same agent re-enters draft
- Layer: M / I
- Source: [17] "Three things to hold onto"; [05] section 9.
- Evidence: "There is no refine state. There is draft, entered again." Reason: "The generator and the refiner are the same agent doing the same thing - produce the next attempt". Cost avoided: "Collapsing them removes a state, an edge, and the duplicated objective that always drifted apart from its twin."
- Confidence: High.

## F9.6 Key mechanism: nothing is cleared; freshness is a size() comparison
- Layer: M / I
- Source: [17] "Three things to hold onto" and YAML guard comments; [05] section 9.
- Evidence:
  - "Not one assign: { x: "" } in the file."
  - "Freshness becomes a size comparison, and that's the load-bearing detail."
  - Meaning: "size(drafts) > size(verdicts) means "a draft is waiting to be judged"; size(verdicts) == size(drafts) means "the newest draft has been judged.""
  - [05]: "when you stop destroying state to re-arm a loop, every test becomes a comparison, and it has to be on both sides of the loop."
- Confidence: High.

## F9.7 Stale-verdict hazard if the equality guard is missing
- Layer: M / I
- Source: [17] "Three things to hold onto" (third bullet) and "Walking two refinement cycles".
- Evidence: "Presence stops being a signal the moment nothing is destroyed: on re-entry to critique, verdicts is still non-empty and its requirement is instantly satisfied by the previous pass's verdict." Consequence: the machine would "park in the wrong state, having silently reused a stale judgement."
- Confidence: High.

## F9.8 BUDGET: retry budget read off the data (special attention)
- Layer: M / I
- Source: [17] "What the reducer does to the failure points", "Refinement loops can continue indefinitely"; YAML guards; [05] section 9.
- Evidence:
  - "size(context.drafts) < 4 is the retry budget, and it reads straight off the accumulated data - there is no attempts counter to increment"
  - Author's rationale: "the loop's own record is its odometer."
  - Second bound: "verdicts is an enum where accept exits, and policy.maxIterations caps the total exchange count for headless runs." YAML: `maxIterations: 6` with comment "hard cap on the draft/critique/refine cycle (headless)".
  - Three outcomes named by the author: "The loop converges, hits the budget and lands in stalled, or trips the policy."
  - Guard shape in YAML: ship when newest verdict is accept; back to draft when newest is revise and drafts < 4; stalled when drafts >= 4 (all gated on size(verdicts) == size(drafts)).
- Confidence: High.

## F9.9 STALLED state (special attention)
- Layer: M
- Source: [17] "What the reducer does to the failure points", "Quality can plateau despite additional iterations"; YAML state `stalled`; [05] section 9.
- Evidence:
  - "a declared stalled state the budget edge routes into, which ships the best draft with an explicit note on what's unresolved rather than burning the remaining cycles."
  - "The plateau is a declared outcome, not a silent waste."
  - YAML state description: "Quality has plateaued - the critic found no material improvement across iterations."
  - The critic can see plateau because its objective carries prior critiques: "'I said this two rounds ago and it still isn't fixed' is a fact on the page rather than something the critic would have to be trusted to remember"
  - Author correction: "An earlier version of this machine offered a stalled verdict the critic had no evidence to justify."
- Confidence: High.

## F9.10 Stated reducer answers for the other failure points
- Layer: M
- Source: [17] "What the reducer does to the failure points"; [05] section 9.
- Evidence:
  - Nitpicking critic: "The machine can't stop a critic from nitpicking - that's a quality-of-the-critic problem. But it can make the nitpicks legible and bounded."
  - Optimizing for the critic: "The critiques are the how (what to fix); the spec is the what (what good means)." Anchor: "The shared anchor is the spec field."
  - Weak criteria: "Weak criteria are visible in the first state's context, not discovered after a loop of weak criticism."
- Confidence: High.

## F9.11 Author's stated trade-off and a correction
- Layer: I
- Source: [17] last two paragraphs before "The whole thing, one idea".
- Evidence:
  - Trade-off: "One honest trade-off, and it's a DSL one: the refiner re-produces the draft; it doesn't edit it in place." Cost: "the store grows by a full artifact per pass." Mitigation: "If your artifacts are large, declare the element type blob so each version is claim-checked".
  - Correction: "Worth correcting one thing an earlier version of this article said, because it's load-bearing elsewhere: assign is not literal-only." Remaining limit: "there's no diff-apply primitive, so an expression can compute a new value but not patch an old one in place."
- Confidence: High.

---

# PATTERN 10: AUCTION / CONTRACT-NET

## F10.1 Definition
- Layer: BP
- Source: [05] section 10; [18] "The pattern".
- Evidence: "A task is advertised to a pool of candidate agents, which compete or bid for the work based on factors such as capability, confidence, cost, latency, availability, model quality, or tool access." And: "This makes assignment itself a dynamic optimization problem rather than a static routing rule."
- Confidence: High.

## F10.2 Good for
- Layer: BP
- Source: [18] "The pattern".
- Evidence: "Large heterogeneous agent pools; Cost optimization; Dynamic resource allocation; Model routing; Systems where agents have different capabilities or availability"
- Confidence: High.

## F10.3 Failure points as listed
- Layer: BP
- Source: [18] "The pattern".
- Evidence: "Agents may misrepresent or poorly calibrate confidence; Bidding introduces coordination overhead; Poor scoring functions select the wrong agent; Cheapest or fastest agent is not necessarily the best; Bids can become difficult to compare across heterogeneous agents"
- Confidence: High.

## F10.4 Key mechanism: one writer-keyed map for all bids
- Layer: M / I
- Source: [18] "Three things to hold onto"; [05] "Many contributors, one field".
- Evidence: "The bid pool is one field, not one field per bidder." Join: "Three requirements name the same field with three different sources, so each is satisfied only by its own candidate - the join is the requirement set". YAML comment: "No join guard - the six required map entries are the join." Cost of a fourth bidder: "adding a fourth candidate adds a requirement, not a guard."
- Confidence: High.

## F10.5 Key mechanism: run-time delegate from the award (from:{agent:'{{ctx}}'})
- Layer: M / I
- Source: [18] "Three things to hold onto" (third bullet); YAML `execute` state; [05] section 10.
- Evidence: "from: { agent: "{{ context.winner }}" } resolves against live context when the state is entered, so the machine summons whichever candidate won." Effect: "That's what collapses three near-identical run_* states into one". Pool enforcement: "The pool is declared once, as the enum on winner, and the enum is what enforces it". [05]: "Adding a fourth bidder adds a value to an enum, not three lines of graph."
- Confidence: High.

## F10.6 Typed comparable bids and the "contract" framing
- Layer: M
- Source: [18] "What the reducer does to the failure points" (last failure point); [05] section 10.
- Evidence: "That's the contract-net idea made literal: the contract is the field's type, and every candidate signs the same one." Structure: "bids is a map, of: string and confidence is a map, of: enum".
- Confidence: High.

## F10.7 Scoring is still an LLM turn (stated limit), with a dial toward guards
- Layer: M
- Source: [18] "What the reducer does to the failure points", after "Poor scoring functions select the wrong agent"; [05] section 10.
- Evidence: "Worth being clear about what's not declared here: the scoring is still an LLM turn reading a map." Dial: "an edge guarded on confidence['agent:premium'] == 'high' is a real, pure, readable predicate". Reason it stays in the prompt: ""quality matters more than cost, deadline is tomorrow" is not a predicate."
- Confidence: High.

## F10.8 Calibration and overhead: stated limits
- Layer: BP / M
- Source: [18] "What the reducer does to the failure points".
- Evidence:
  - "The machine can't stop a candidate from over-bidding - that's a calibration problem."
  - "The fix is calibration in the candidate agents (or a validate predicate on the bid), not a different auction shape."
  - Overhead: "If the overhead isn't worth it for a given task, you don't run the auction; you route statically (the supervisor pattern)."
- Confidence: High.

## F10.9 Author's two stated trade-offs (one-level collections; single round)
- Layer: M
- Source: [18] "Two honest trade-offs" paragraph; [05] section 10 does not cover these.
- Evidence:
  - "First, collections are one level deep by design - of: names a scalar - so a bid can't be a record." Consequence: "That's why confidence lives in its own parallel map rather than nested inside the bid".
  - "Second, this is a single-round auction: the candidates bid once, the orchestrator awards, and the work happens."
  - Multi-round re-arm cost: "the bid maps have to be cleared on the loop-back edge (remove: { bids: "*" }), because a map entry that's already there satisfies its requirement".
- Confidence: High.

---

# PATTERN 11: EVENT-DRIVEN / PUBLISH-SUBSCRIBE

## F11.1 Definition
- Layer: BP
- Source: [05] section 11; [19] "The pattern".
- Evidence: "Agents subscribe to event types and react independently when relevant events are emitted." And: "The overall workflow emerges from subscriptions, event contracts, and reactions, making the pattern naturally asynchronous and loosely coupled."
- Confidence: High.

## F11.2 Good for
- Layer: BP
- Source: [19] "The pattern".
- Evidence: "Long-running systems; Asynchronous workflows; Distributed agent systems; Monitoring and reactive automation; Enterprise event architectures; Systems where agents should act only when relevant changes occur"
- Confidence: High.

## F11.3 Failure points as listed
- Layer: BP
- Source: [19] "The pattern".
- Evidence: "Event storms; Duplicate processing; Race conditions; Unclear causality; Difficult global reasoning; Accidental feedback loops"
- Confidence: High.

## F11.4 HONESTY STATEMENT 1: the pattern is half real (author splits inside from outside)
- Layer: BP
- Source: [19] "A little background", paragraphs 1-4; [05] section 11 caveat paragraph.
- Evidence:
  - "this one is the most surprising - and the one where I have to draw a line through the middle of the pattern and tell you which half is real."
  - "The half that's real is almost tautological: the reactive reducer is an event-driven system, internally."
  - "The half that isn't real is the outside."
  - "So read this article as: the internal event system is the load-bearing claim and it's sound; the external legs are declared intent with the transport not yet built."
- Confidence: High (explicit, author's own).

## F11.5 HONESTY STATEMENT 2: emit/send only write a log line (verbatim, separate finding)
- Layer: I
- Source: [19] "A little background" paragraph 3 and "Three things to hold onto"; [05] section 11 caveat paragraph.
- Evidence:
  - [19]: "And the emit/send actions, which are genuine DSL syntax the reducer genuinely executes, currently only write a log line. No bus, no webhook, no subscriber. That's true on the interactive runtime and on the headless targets alike."
  - [19]: "emit does not currently reach anything."
  - [19]: "There is no bridge from an emit action to the application bus, on the interactive runtime or on the headless targets."
  - [05]: "emit/send, while real DSL the reducer executes, currently only write a log line - no bus, no webhook, on either the interactive runtime or the headless targets."
  - [05]: "Read the emits as a declared publish contract with the transport unbuilt."
- Confidence: High (explicit, repeated in two files).

## F11.6 HONESTY STATEMENT 3: no external ingress exists (verbatim, separate finding)
- Layer: I
- Source: [19] "A little background" paragraph 3 and "Three things to hold onto" (final paragraph); [05] section 11.
- Evidence:
  - [19]: "There is no stream tap, no webhook receiver, no sensor: an external event reaches a machine only because something in-process calls updateContext."
  - [19]: "the watcher is an agent, not a stream. Nothing taps a metrics pipeline."
  - [19]: "watch parks and asks an LLM what the metric is doing, which is fine for illustrating the topology and is not monitoring."
  - [19]: "A real ingress leg means an external adapter turning stream events into context writes on a specific instance".
  - [19]: "Neither leg exists yet, and this is the one pattern in the series where that absence is the pattern itself rather than a detail at the edge of it."
  - [05]: "Nothing taps a stream (the "watcher" is an agent turn)".
- Confidence: High (explicit).

## F11.7 HONESTY STATEMENT 4: emits are documentation of intent; how cheap the fix would be
- Layer: I
- Source: [19] "Three things to hold onto", third bullet.
- Evidence: "So metric.breach is not something a dashboard can subscribe to today; it's a declaration of the publish contract with the transport unbuilt." And: "treat every emit in this article as documentation of intent rather than an integration point." Author's cost estimate: "Wiring it is the cheapest of the gaps here - the action exists and a bus exists, so it's a middleware, not a design".
- Confidence: High. (The "cheapest of the gaps" estimate is the author's opinion, not a measured claim.)

## F11.8 HONESTY STATEMENT 5: the author's closing verdict on fit
- Layer: BP
- Source: [19] "What the reducer does to the failure points", closing paragraph "Where this leaves the pattern, honestly", and the final paragraph before "Up next".
- Evidence:
  - "Externally, it is not yet an event system at all: no ingress adapter, and emit logging rather than publishing."
  - "Of the thirteen patterns in this series, this is the one where the reducer's fit is a genuine claim about the inside and an open item about the outside"
  - "I'd rather it be marked that way than counted as a clean fit on the strength of syntax that parses."
  - "The emits in that table went to a log, and the watcher was an agent rather than a stream."
  - Why the author states it: "I'd rather say that here than let you find it by wiring a dashboard to metric.breach and waiting."
- Confidence: High.

## F11.9 What the author says IS real: the reducer as internal bus
- Layer: M
- Source: [19] "A little background" paragraph 2; "Three things to hold onto", first bullet.
- Evidence: "Every context write is an event. The microstep loop is the reaction. The guards are the subscriptions. The context store is the bus." And: "The reactors are summoned by the machine - they don't poll and they don't subscribe in code."
- Confidence: High.

## F11.10 Key mechanism: reactors are concurrent subscribers, keyed map (and a self-correction)
- Layer: M / I
- Source: [19] "Three things to hold onto", second bullet; YAML `react` state.
- Evidence: "All three requirements name the same reactions field with three different sources, so all three are summoned concurrently and each entry is keyed by whoever wrote it." Correction: "An earlier version of this article chained them state-to-state, which reads as pub-sub but is really a pipeline wearing its clothes."
- Confidence: High.

## F11.11 Key mechanism: consume-the-event dedup has two halves
- Layer: M / I
- Source: [19] "What the reducer does to the failure points", "Duplicate processing"; YAML `react -> watch` edge; [05] section 11.
- Evidence: "Both halves of that matter: clearing the scalars re-arms the routing, and clearing the map re-arms the asks". Rule: "nothing that gates a cycle should survive the cycle." YAML: `assign: metric: "", status: ""` and `remove: reactions: "*"`.
- Confidence: High.

## F11.12 Key mechanism: status as enum, not bool (author self-correction on details)
- Layer: I
- Source: [19] after the YAML ("Two details in there...").
- Evidence: "Two details in there are worth pointing at before the walk-through, because both are places an earlier draft of this article was wrong." Detail 2: "status is an enum rather than a settled: bool, because a boolean's presence is what a requirement checks, and false is present." Detail 1: "emit sits in onTransition and exit, not in an entry block on a transition".
- Confidence: High.

## F11.13 Storm bound and microstep cap
- Layer: I
- Source: [19] "What the reducer does to the failure points", "Event storms".
- Evidence: "the microstep cap (128) bounds the reactions per event." External: "A storm of external events (the bus floods the watcher) is bounded by policy.maxIterations on the watch/react cycle and by the watcher's own rate". Author's limit: "external storms are a watcher-throughput problem you can see in the parked record". YAML: `maxIterations: 20`.
- Confidence: High. (The number 128 appears in [19] only, not in [05].)

## F11.14 Accidental safety property of the missing external legs
- Layer: BP
- Source: [19] "Accidental feedback loops"; [05] section 11.
- Evidence: "It's also worth noticing that the absent external legs are, right now, an accidental safety property: an emit that reached a bus that fed a watcher would be exactly the cross-process cycle nothing in the machine can see."
- Confidence: High.

## F11.15 Causality, races, global state (stated reducer answers)
- Layer: M
- Source: [19] "What the reducer does to the failure points".
- Evidence: Races: ""Race conditions" is a concurrency problem; here it's a queue". Causality: "The causal chain is the edge sequence plus the writer-keyed map - journaled, replayable, not reconstructed from a bus trace." Global state: "you read the store." Backstop: "The no-progress guard is the backstop within a single advance: a re-entry with unchanged context stops."
- Confidence: High.

## F11.16 Unbuilt claim: mesh of reducers across processes
- Layer: BP / I
- Source: [19] closing paragraph.
- Evidence: "Composing many reducers into a mesh - each with its own store, reacting to each other's events across process boundaries - is the right shape and the headless targets are the right substrate for it, but the connective tissue is unbuilt in both directions."
- Confidence: High (author states it as unbuilt).

---

# PATTERN 12: QUORUM / VOTING

## F12.1 Definition
- Layer: BP
- Source: [05] section 12; [20] "The pattern".
- Evidence: "Multiple agents independently evaluate the same question, classification, or decision, and their outputs are aggregated using a voting or quorum rule. The agents may never communicate with one another"
- Confidence: High.

## F12.2 Good for
- Layer: BP
- Source: [20] "The pattern".
- Evidence: "High-confidence decisions; Classification; Validation; Reducing single-model variance; Safety or quality gates; Ensemble-style reasoning"
- Confidence: High.

## F12.3 Failure points as listed
- Layer: BP
- Source: [20] "The pattern".
- Evidence: "Correlated agents produce correlated errors; Majority agreement does not guarantee correctness; Duplicate inference increases cost; Confidence scores may be poorly calibrated; Weak or homogeneous agent diversity limits the value of voting"
- Confidence: High.

## F12.4 Key mechanism: the quorum is a guard, not a prompt (size(filter()))
- Layer: M / I
- Source: [20] "A little background" and "Three things to hold onto" (third bullet); YAML `tally` state; [05] section 12.
- Evidence:
  - "The reducer removes that trust step. The votes are typed context fields. The majority is a CEL expression on the join edge." and "The quorum is applied, not performed."
  - "The guards count - size(votes.filter(k, votes[k] == 'approve')) >= 2 is the quorum rule itself, not an enumeration of the winning combinations."
  - Panel size: "Change the panel to five and the rule becomes >= 3; the old shape needed ten disjuncts."
  - `tally` is "a pure decision node: no agent, no requires, no objective."
  - Agent role after outcome: "The chair only acts after the outcome, to compose the rationale from votes already in the store."
- Confidence: High.

## F12.5 Key mechanism: independence is structural
- Layer: M
- Source: [20] "Three things to hold onto" (first bullet); "Weak or homogeneous agent diversity" paragraph.
- Evidence: "Independence is structural, not a prompt." And: "The "agents may never communicate" property the pattern wants isn't a convention you hope the models follow - it's the topology." And: "the voters are summoned concurrently on the same snapshot, with no access to each other's votes."
- Confidence: High.

## F12.6 Key mechanism: one writer-keyed ballot map prevents double or proxy votes
- Layer: M / I
- Source: [20] "Three things to hold onto" (second bullet); [05] section 12 ("Voting twice, or on someone else's behalf").
- Evidence: "votes is a map and every entry is keyed by whoever actually wrote it - the runtime forces the key and rejects an explicit one on a field a named requirement is gathering, so a reviewer can't file a vote under a colleague's name or vote twice." [05] labels this as added: "Not on the original list, but it's what a single shared ballot field would invite."
- Confidence: High.

## F12.7 TYPING NOTE: integer vs number (special attention, verbatim)
- Layer: I
- Source: [20] after "Three things to hold onto"; [05] section 12, "Confidence scores may be poorly calibrated".
- Evidence:
  - [20]: "One typing note, because it's the kind of thing that fails quietly: confidence is an integer (0-100), not a decimal."
  - [20]: "There is no number type - a field declared with one falls through to string, and the write succeeds, and then a guard comparing it numerically errors, and an errored guard is treated as not firing."
  - [20]: "The failure surfaces as an edge that mysteriously never takes. Integers compare; strings that look like numbers don't."
  - [20] again in the calibration paragraph: "a field declared number would land as a string and the arithmetic guard would error, which the reducer treats as "does not fire"."
  - [05]: "Mind the typing: there is no number type, and a field declared with one lands as a string, whereupon the arithmetic guard errors and an errored guard simply doesn't fire."
  - YAML: `confidence: { type: map, of: integer }` with comment "0-100, keyed the same way".
- Confidence: High (explicit in both files).

## F12.8 Weighted quorum and "suspiciously unanimous" tripwire as guards
- Layer: M
- Source: [20] "What the reducer does to the failure points".
- Evidence: Weighted rule: "confidence['agent:security'] >= 80 && votes['agent:security'] == 'approve' for a high-confidence veto, or a sum over the approvers for a threshold rule." Tripwire: "size(context.confidence.filter(k, context.confidence[k] >= 90)) == 3 is a perfectly good "suspiciously unanimous" tripwire". Rationale: "The type is what makes this work".
- Confidence: High.

## F12.9 Stated limits: the reducer cannot fix correlation or guarantee correctness
- Layer: BP
- Source: [20] "What the reducer does to the failure points"; [05] section 12.
- Evidence:
  - "The reducer can't fix correlation - if all three reviewers are the same model with the same blind spot, they'll make the same error and the majority will be confidently wrong."
  - "a 2/3 majority is a confidence signal, not a correctness proof."
  - "The mechanism guarantees independent evaluation; the diversity of the voters is your design choice"
  - Cost: "Yes - quorum is duplicated inference, by design." and "If the cost isn't worth it for a given decision, you don't run a quorum; you run a single reviewer."
  - Latency: "the wall-clock cost is the slowest voter, not the sum".
- Confidence: High.

## F12.10 Author's stated trade-off: one-shot quorum; re-vote requires clearing
- Layer: M
- Source: [20] "Two honest trade-offs" paragraph.
- Evidence: "This is a one-shot quorum: the voters vote once, the majority fires, the decision is delivered." Boundary: "If you need the voters to react to each other's positions, you want debate, not quorum." Re-arm cost: "the loop-back edge has to remove: { votes: "*" } and the same for confidence, because an entry that survives the round satisfies its requirement". Rule: "Accumulated state is only safe where it's meant to accumulate."
- Confidence: High.

---

# PATTERN 13: DYNAMIC TEAM FORMATION

## F13.1 Definition
- Layer: BP
- Source: [05] section 13; [21] "The pattern".
- Evidence: "Instead of assigning work to a fixed group of agents, the system assembles a temporary team based on the needs of the current task." And: "Team composition can therefore evolve alongside the problem itself."
- Confidence: High.

## F13.2 Good for
- Layer: BP
- Source: [21] "The pattern".
- Evidence: "Large agent registries; Heterogeneous capabilities; Organizations with many specialists; Tasks whose requirements emerge dynamically; Mission-oriented agent systems; Workloads that need different skills at different stages"
- Confidence: High.

## F13.3 Failure points as listed
- Layer: BP
- Source: [21] "The pattern".
- Evidence: "Poor team selection; Excessive discovery and negotiation overhead; Redundant specialists; Loss of context when team composition changes; Team churn can undermine ownership and continuity"
- Confidence: High.

## F13.4 Key mechanism: roster as an enum list = the registry as a type
- Layer: M / I
- Source: [21] "Three things to hold onto" (first bullet); YAML `roster: { type: list, of: enum, values: [researcher, coder, qa, devops, writer] }`.
- Evidence: "The registry is a type, not an agent block. roster is a list whose element type is an enum over the five specialists." Enforcement: "recruiting datascientist is rejected with the legal values, not discovered later as a summons nobody answers." Why only builder is an agent: "agents: confers authority to drive the machine, and a recruited specialist doesn't drive anything; it answers an ask."
- Confidence: High.

## F13.5 Key mechanism: one phase state, run-time delegate (from:{agent:'{{ctx}}'})
- Layer: M / I
- Source: [21] "Three things to hold onto" (second bullet); [05] "structural note"; YAML `phase` state.
- Evidence: "There is one phase state, and its delegate is chosen at run time. from: { agent: "{{ context.current }}" } resolves against live context when the state is entered." Effect: "the same state, a different principal each pass." [05]: "A mission of two and a mission of four traverse the same graph - the topology stops scaling with the registry."
- Confidence: High.

## F13.6 Key mechanism: derived turn order (no cursor field)
- Layer: M / I
- Source: [21] "Three things to hold onto" (third bullet); YAML `next` state (`from: system`, `extract: "context.roster[size(context.outputs)]"`).
- Evidence: "The turn order is derived from the data, not tracked beside it." And: "There's no cursor field to advance, no counter to keep in sync with the roster it's counting, and no way for the two to disagree." The `next` state: "a CEL expression, no agent, no LLM turn." Loop-back guard in YAML: `size(context.outputs) < size(context.roster)`, release when `>=`; `onTransition` re-arms the pick with `assign: current: ""`.
- Confidence: High.

## F13.7 Over-recruitment cap as a real edge (size comparison)
- Layer: M
- Source: [21] "What the reducer does to the failure points", "Redundant specialists"; YAML `overstaffed` state.
- Evidence: "size(context.roster) > 4 is a real edge into a real state: over-recruit and the mission doesn't start, the roster is cleared, and the builder is sent back to recruit again with the oversized roster quoted back at it." Cost: "an over-recruitment costs one builder turn rather than four specialist turns."
- Confidence: High.

## F13.8 Stated limits: fit cannot be judged; selection stays a judgment call
- Layer: BP
- Source: [21] "Redundant specialists" and "Poor team selection" paragraphs.
- Evidence: "What the cap can't judge is whether a within-budget roster contains someone the mission didn't need". And: "Size is; fit isn't." And: "Beyond that the selection is a judgment; the routing is exact."
- Confidence: High.

## F13.9 Context lives in the store, not the members
- Layer: M
- Source: [21] "Loss of context when team composition changes"; [05] section 13.
- Evidence: "The context doesn't live in the specialists; it lives in the store, keyed by which specialist produced it. A recruit that's finished and gone hasn't taken anything with it." Also: "the members are interchangeable and the store is not."
- Confidence: High.

## F13.10 AUTHOR'S STATED LIMIT: churn and replacement are not free
- Layer: M
- Source: [21] "Team churn can undermine ownership and continuity"; [05] section 13.
- Evidence: "Churn is where this shape gives something up, and it's worth being straight about it." And: "Appending a specialist mid-mission works and extends the run; replacing one doesn't, because the index of a delivered contribution is already spent." Fix described: "a replacements map from original to substitute, with the pick reading through it, or a dropped list the pick skips over." Cost: "it isn't free, and the version above doesn't have it."
- Confidence: High.

## F13.11 Author's trade-offs: one-level collections; relation to the auction
- Layer: M
- Source: [21] "Two trade-offs to name".
- Evidence:
  - "Collections are one level deep - of: names a scalar - so a roster entry is a name, not a record with a capability vector and a cost."
  - Workaround: "a parallel map keyed the same way (the auction article does exactly that with bids and confidence)".
  - "The auction's write is a scalar enum; the team's is a list of them. Selection and composition turn out to be the same machinery with a different arity."
- Confidence: High.

## F13.12 Author's self-correction on the registry description
- Layer: M
- Source: [21] "Three things to hold onto", first bullet.
- Evidence: "This is the distinction the earlier version of this article collapsed, which is why it read as a five-specialist registry with three of them wired."
- Confidence: High.

## F13.13 Discovery overhead is one builder turn
- Layer: BP
- Source: [21] "Excessive discovery and negotiation overhead".
- Evidence: "There's no multi-round negotiation between the builder and the specialists - the specialists are summoned after the roster is set, and they don't negotiate." And: "If the overhead isn't worth it for a given mission, you don't run dynamic team formation; you run a fixed team".
- Confidence: High.

---

# CROSS-PATTERN ITEMS FROM [05] (patterns 8-13 only)

## FX.1 Category grouping for patterns 8-13
- Layer: BP
- Source: [05] "A useful mental model" table.
- Evidence: Table rows read: "Planning | Planner / Executor"; "Reasoning | Debate / Consensus, Quorum / Voting, Generator / Critic / Refiner"; "Delegation | Peer Handoff / Swarm, Auction / Contract-Net, Dynamic Team Formation"; "Coordination | Blackboard / Shared-State, Event-Driven / Publish-Subscribe". Caveat: "The categories aren't mutually exclusive - real systems compose several at once."
- Confidence: High.

## FX.2 The three recurring idioms the author names (apply to 8-13)
- Layer: M
- Source: [05] final section.
- Evidence:
  - "Many contributors, one field. When N actors contribute the same kind of thing - votes, bids, findings, critiques, positions - that's one map field, not N scalar fields."
  - "Accumulate what you keep; consume what re-arms."
  - "When nothing is destroyed, every test becomes a comparison."
  - Limit: "None of these is a feature you turn on."
- Confidence: High.

## FX.3 Reducer's three rules (context for all patterns)
- Layer: BP / M
- Source: [05] "Why the reactive reducer?". (Included only as framing for the quotes above, not as a gathered pattern.)
- Evidence: "Agents advance the machine by writing context, never by requesting a transition. An actor's only lever is to contribute a value." And: "A microstep loop fires the first eligible, guard-passing edge until the machine parks - durable, journaled, restart-safe."
- Confidence: High.

## FX.4 Observed wording differences between [05] and the per-pattern files (recorded, not resolved)
- Layer: M
- Source: [05] vs [16]-[21].
- Evidence:
  - Microstep cap: only [19] gives the number ("the microstep cap (128)"); [05] says "the microstep cap bounds the reactions per event."
  - Pattern 12: [05] says "a five-voter panel is a different number, not ten more disjuncts"; [20] says "the old shape needed ten disjuncts" and "a five-reviewer panel would be the same expression with a 3".
  - Pattern 8 trade-off (plan as text, not a task graph) appears only in [16]; [05] omits it. Pattern 10 trade-offs (one-level collections, single round) appear only in [18]. Pattern 12 one-shot trade-off appears only in [20]. Pattern 13 churn limit appears in both.
  - Dates "Sep 10/11/12, 2026" appear in the headers of [18]-[20] only; [16], [17], [21] and [05] carry none.
- Confidence: High that the differences exist (direct comparison of the files). No judgment on which text is authoritative.

---

## Declarative Conclusions
Not applicable: external text sources, no local human sources (per plan).

## Rejected Information

| # | Information | Source | Rejection Reason | Re-include If |
|---|---|---|---|---|
| 1 | Patterns 1-7 sections (Sequential Pipeline through Blackboard) | [05] sections 1-7 | Outside this instance's assignment (patterns 8-13 only); another category covers them | The synthesis phase needs a cross-pattern comparison |
| 2 | "Why the reactive reducer?" intro beyond the framing in FX.3 | [05] intro | General series framing, not pattern 8-13 content | The verification phase needs the full rule set |
| 3 | Full FSM YAML listings (state descriptions, prompt strings, policy numbers other than maxIterations) | [16]-[21] | Mechanism is captured by quotes; full YAML is better read from the files themselves | A downstream step needs to port or reproduce a machine |
| 4 | Worked walk-throughs (rate-limit re-plan, webhook retry, translation auction, metric breach, deployment gate, mission team) | [16]-[21] "Walking ..." sections | Illustrative examples of the mechanisms already captured; no new limits or claims | A downstream step needs concrete sequences as test cases |
| 5 | "The whole thing, one idea" summary tables and closing paragraphs | [16]-[21] | Restate findings above; closing marketing-style summaries | Not expected |
| 6 | "Up next" links and series navigation | [05], [16]-[21] | Navigation only | Not expected |
| 7 | Links to the other 13 per-pattern posts and the series intro ("Read the full article") | [05] | Plan forbids following links outside the 18-URL list | The URL list is widened |
| 8 | Patterns' diagrams (Mermaid) | [05]-[21] | Flattened or omitted by the paste; edge labels adequately covered by YAML and prose | The original posts are re-fetched with diagrams intact |
| 9 | References to patterns 1-7 inside the 8-13 articles (e.g. supervisor, hierarchical, debate, blackboard cross-references) | [16]-[21] | Out of this instance's scope | Cross-pattern synthesis is requested |

## Actor Relevance
Not applicable: the plan lists no actors ("None - research is actor-agnostic").
