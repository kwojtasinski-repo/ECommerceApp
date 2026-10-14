# Findings: bm-patterns-1-7-raw

Scope: patterns 1-7 of Brad Murry's "13 Multi-Agent Orchestration Patterns, Implemented via the Reactive Reducer" series, plus the index's closing category table and "three idioms". Gathered from full-text local files (no web).

Source key (all under `C:\Projekty\ECommerceApp\docs\research\2026-10-14-agent-patterns-bradmurry\analysis\sources-raw\`):
- IDX = `05-reactive-reducer-patterns-13.md`. URL (from header): https://bradmurry.com/software/reactive-reducer-patterns/
- P1 = `09-pattern-01-sequential-pipeline.md`; P2 = `10-pattern-02-parallel-fanout-fanin.md`; P3 = `11-pattern-03-supervisor-router.md`; P4 = `12-pattern-04-hierarchical-manager-worker.md`; P5 = `13-pattern-05-peer-handoff-swarm.md`; P6 = `14-pattern-06-debate-consensus.md`; P7 = `15-pattern-07-blackboard-shared-state.md`.
- URL caveat: the P1-P7 file headers state "URL NOT in the user's 18-URL list" and give no URL. Only the article title is recorded. Per-pattern URLs are therefore not available and are not invented; the citation is file name plus quote. These per-pattern articles were pasted by the user on 2026-10-14, as stated in the headers.
- Provenance note: P5 has one editorial omission (the `technical` and `security` YAML states are replaced by a marked placeholder). IDX has its diagrams flattened.
- Terms: "Aegis FSM" is the author's own implementation; "reactive reducer" is the author's design. Layers: Big Picture (BP), Modeling (M), Implementation (I).
- Per-pattern quotes below come from the pattern article unless the Source line says IDX.

---

## F0. Series premise: why the reactive reducer (three rules, payoff)
- Source: IDX, section "Why the reactive reducer?"
- Evidence: "Agents advance the machine by writing context, never by requesting a transition. An actor's only lever is to contribute a value."
- Evidence (payoff): "token cost is bounded by the design (a verdict enum, maxIterations, a retry budget read off the data itself) rather than by how well the models behaved."
- Evidence (rule 1): "context is the single source of truth - every artifact and decision is a typed field in a shared store. The handoff is a context write, not a message."
- Detail: Rule 3 is a microstep loop that fires the first eligible, guard-passing edge until the machine parks ("durable, journaled, restart-safe"). The same FSM is said to run in an interactive runner or compiled to Mutiny / Flink.
- Layer: BP, M
- Confidence: High - explicit statements in the index.

## F0b. Index closing: the mental-model category table
- Source: IDX, section "A useful mental model"
- Evidence: "The thirteen patterns group by the primary orchestration problem they solve:" followed by the table. Patterns 1-7 sit in these categories:

| Category | Patterns (within 1-7) |
|---|---|
| Flow | Sequential Pipeline, Parallel Fan-Out / Fan-In |
| Authority | Supervisor / Router, Hierarchical Manager / Worker |
| Delegation | Peer Handoff / Swarm (also Auction / Contract-Net and Dynamic Team Formation, outside 1-7) |
| Reasoning | Debate / Consensus (also Quorum / Voting and Generator / Critic / Refiner, outside 1-7) |
| Coordination | Blackboard / Shared-State (also Event-Driven / Publish-Subscribe, outside 1-7) |

- Planning (Planner / Executor) is a category outside 1-7.
- Author's caveat: "The categories aren't mutually exclusive - real systems compose several at once."
- Author's summary claim: "the agents do the judgment, the context store holds the state, the guards apply the rules, and the reducer is the only thing that moves the machine."
- Layer: BP
- Confidence: High - verbatim table.

## F0c. The "three idioms" recurring across the patterns
- Source: IDX, "Three idioms recur across almost all thirteen"
- Idiom 1, "Many contributors, one field". Evidence: "the join is the requirement set, the attribution is the key, and 'how many have answered' is size()."
  - Detail: N contributors give one map field, not N scalar fields. Entries are keyed by whoever actually wrote them, and the runtime forces the key. A field per contributor "makes the panel's size part of the graph, so changing it means editing guards."
- Idiom 2, "Accumulate what you keep; consume what re-arms". Evidence: "anything that gates a loop has to be destroyed for the loop to re-arm - and the thing you'd naturally destroy is usually the record you wanted."
  - Mechanism: a scalar trigger blanked on the edge, plus a list or map that only grows, appended in an exit action. Exit "runs after the edge is chosen and before it fires, so the trigger is still readable".
  - Corollary: "Most of the 'loop that runs and does nothing' bugs in this series are one half of that split missing."
- Idiom 3, "When nothing is destroyed, every test becomes a comparison". Evidence: "Freshness becomes size(drafts) > size(verdicts), and the retry budget becomes size(drafts) < 4 rather than a counter beside the data that can disagree with it."
  - Detail: "has(x) stops being a signal - it's true from the first write forever." The author notes "Both sides of the loop have to compare".
- Author's closing note: "None of these is a feature you turn on. They're what typed context plus pure guards make natural once the scalar-shaped workarounds are gone."
- Layer: M, I
- Confidence: High - explicit section.

---

# PATTERN 1: Sequential Pipeline

## F1. Definition, good-for, failure points (P1; matches IDX)
- Source: P1, "The pattern"; also IDX section 1.
- Evidence (definition): "Each agent owns a distinct stage of the workflow and passes its output forward as the input to the next stage."
- Good for: "Work with a natural ordering; Research → design → implementation → review; ETL-like processing; Document generation; Multi-stage transformation pipelines".
- Listed failure points: "Early mistakes compound downstream; Context degrades during handoffs; Slowest agent determines latency; Recovery may require replaying earlier stages".
- Author's framing: "The pattern itself is boring. That's the point - it's the shape of most real work."
- Layer: BP
- Confidence: High - verbatim lists.

## F1b. Stated reducer answers to the four failure points (P1, IDX)
- Source: P1, "What the reducer does to the failure points".
- Early mistakes: "Every stage's output is a named context field, so a bad value is traceable to the exact stage that wrote it." The back-edge is the containment mechanism: "the rework agent gets the actual feedback, not a paraphrase of a paraphrase."
- Context degradation: "Here the handoff is the artifact itself." Templating "is claim-safe and uncapped, unlike the truncated CONTEXT block". Big outputs are declared `blob` and claim-checked.
- Latency: "True, and the FSM makes it visible instead of buried." Each stage is a park, and the parked record is journaled.
- Recovery: "A parked machine is a replay point. The (state, context) pair is the complete run". A bad stage does not replay the whole pipeline: the back-edge re-parks at the stage that must redo work.
- Added property: "legality is structural" - an agent cannot skip a stage and an undeclared edge cannot happen.
- Layer: M
- Confidence: High.

## F1c. Key mechanism: stage = state with a requires; handoff = context write; template interpolation
- Source: P1, "The handoff problem" and "The machine".
- Evidence: "A stage is a state with a requires. The owning agent's job is to produce the context field that requirement names. Until that field is set, the machine parks - it doesn't time out, spin, or guess."
- Evidence: "{{ context.<field> }} interpolates the full value into the next agent's instruction, so the handoff carries the artifact itself, not a summary of it."
- Contrast with message-passing frameworks: "a handoff is a message: agent A writes a summary, agent B reads it. That summary is a lossy compression of A's actual work".
- Layer: M, I
- Confidence: High.

## F1d. Rework back-edge: consume the trigger AND artifact, keep the notes
- Source: P1, YAML comment and paragraph "Notice what the rework edge does".
- Evidence (YAML comment): "consume the trigger AND the artifact, KEEP the notes"
- Evidence: "That consume-the-trigger / keep-the-feedback discipline is what makes the loop reentrant instead of oscillating."
- Detail: the edge review to rework clears `review` and `implemented`, but keeps `review_notes`. The edge rework to review then clears `review_notes` so the next review collects fresh notes.
- Layer: I
- Confidence: High.

## F1e. Author's gotcha: "blank is absent, but false is present"
- Source: P1, paragraph "One thing that bites here".
- Evidence: "blank is absent, but false is present."
- Evidence: "A requirement is satisfied when its field is present, and the emptiness test is per-type - an empty string, an empty list, an empty map all read as absent".
- Consequence stated: a bool reset to false "would leave it present, rework would be satisfied the moment it was entered". The author calls it "a silent failure - no error, just a rework state nobody works in."
- Fix: make a re-armable field a string or enum and blank it. Also "the one shape in the DSL where the intuitive modelling choice is the wrong one."
- Internal inconsistency noted (author's own text): the YAML comment says `implemented` is "what was built - NOT a bool", but the closing table says "implemented = true". The author does not reconcile this.
- Layer: I
- Confidence: High for the quotes. The inconsistency is a Medium-confidence observation by this gatherer, from reading both passages.

## F1f. Policy settings in the pipeline FSM
- Source: P1, YAML `policy`.
- Evidence: `maxExchangesPerStage: 30`, `maxExchangesPerAgentPerStage: 15`, `checkpointEvery: 5` (no `maxIterations` in this machine).
- Layer: I
- Confidence: High.

---

# PATTERN 2: Parallel Fan-Out / Fan-In

## F2. Definition, good-for, failure points (P2; matches IDX)
- Source: P2, "The pattern".
- Evidence (definition): "A coordinator decomposes a larger task into independent or mostly independent subtasks and dispatches them to several agents at the same time."
- Evidence: "The pattern trades coordination complexity for lower latency and broader coverage."
- Good for: "Research from multiple sources; Independent analysis; Generating alternatives; Broad search or comparison tasks; Large decomposable workloads".
- Listed failure points: "Bad decomposition creates overlapping or missing work; Outputs can be inconsistent or hard to combine; Aggregator becomes the bottleneck; One slow worker can hold up the join".
- Layer: BP
- Confidence: High.

## F2b. Key mechanism: no fan-out or join primitive - parking and guards
- Source: P2, "A little background" and "The machine".
- Evidence: "In the Aegis FSM, there's no fan-out primitive and no join primitive at all. Both emerge from two things you already have: parking and guards."
- Evidence: "The fan-out is the fanout state's requires list. Three requirements, three different delegate sources. There is no dispatch call, no spawnWorkers(3)".
- Evidence: "The guard is the join - and because the workers write into one map keyed by whoever wrote it, the guard counts rather than enumerating names."
- Concurrent summons: "the runtime raises all three elicitations at once: three A2A summons, fired concurrently". Completion order is irrelevant.
- Layer: M, I
- Confidence: High.

## F2c. The load-bearing flag: `required: false` + guard join
- Source: P2, section "The load-bearing flag: required: false".
- Default behaviour: "By default, a state's requires are an all-required AND-gate on edge eligibility". Three required requirements on one map field "are, by themselves, an exact all-of join".
- Evidence: "when the join isn't all-of, the authoring discipline is: mark the fan-out requirements required: false (they're still elicited on entry - they just don't block eligibility) and let the transition guard be the sole join."
- Author's disclosure: the article writes it this way "even though its join happens to be all-of, because the guard is the general mechanism".
- Example join from the author's real panel-review FSM: `(security && perf && human)  ||  legal_override`.
- Guard variants from the failure-point text: `size(context.findings) >= 2` for two-of-three, an `||` fast-path, and a filtered count for a conditional quorum.
- Layer: I
- Confidence: High.

## F2d. Stated reducer answers to the four failure points
- Source: P2, "What the reducer does to the failure points"; IDX section 2.
- Decomposition: "The decomposition is a staged, inspectable context field produced by a named state". It can be validated, gated by review, or routed back via a back-edge.
- Inconsistent outputs: "Every worker writes into the same typed field, so the outputs are coerced to one shape at the write". The synthesizer sees the whole map, keyed by researcher.
- Aggregator bottleneck: "True, and now it's one state you can see." Parallelizable synthesis is "just... another fan-out state. The pattern composes with itself."
- Slow worker: "The machine doesn't time out, poll, or busy-wait - it parks, and parking is durable". The join is a predicate: "The 'how many must finish' question stops being a framework feature and becomes a predicate".
- Layer: M
- Confidence: High.

## F2e. Dynamic variant: fork() when the count comes from the data
- Source: P2, section "The dynamic variant: fork when the count isn't known".
- Evidence: "the coordinator doesn't fan out to agents, it fans out to forks."
- Detail: the lead stages "a short list of stopIds, not prose - context stays small". `dispatch` calls `fork()` once, one lightweight worker task per item. Workers "run to a concurrency cap and their results return automatically". Re-fork of failures is bounded by `policy.maxIterations: 4`. The join is an `allValid` flag set by the gather agent, and the re-fork edge consumes the trigger with `assign`.
- Source example named: the "real poi-enrich-parallel FSM".
- Author's framing: "the slow/failing worker is a bounded re-dispatch instead of a barrier timeout."
- Layer: I
- Confidence: High. This is single-source and the FSM is the author's own, with no external evidence in the file.

---

# PATTERN 3: Supervisor / Router

## F3. Definition, good-for, failure points (P3; matches IDX)
- Source: P3, "The pattern".
- Evidence: "A central supervisor retains control of the workflow and decides which specialist agent should act next based on the current task and state."
- Good for: "Tool-rich assistants; Specialist agent teams; Customer support systems; Coding agents; Systems where centralized control is desirable".
- Listed failure points: "Supervisor becomes a bottleneck and single point of failure; Bad routing poisons the whole task; Supervisor can consume excessive context; Specialists may become overly dependent on the supervisor's interpretation".
- Layer: BP
- Confidence: High.

## F3b. Key mechanism: hub state with a typed enum "wheel"
- Source: P3, intro and "The machine".
- Contrast with tool-calling: "The routing policy lives in the model's head, which is to say it's emergent, and you find out what it is by reading the trace after the fact."
- Evidence: "The supervisor picks; the machine dispatches. The policy is data you can read before the run, not behavior you discover after it."
- Evidence: "The supervisor never calls an agent. It writes route, and the machine summons the specialist."
- Evidence: "The supervisor's authority is exactly one typed field."
- Evidence: "dispatch is the hub, and it's reentrant."
- Auditability: "the routing table is auditable before the run".
- Layer: M, I
- Confidence: High.

## F3c. Accumulate vs consume: finding is consumed, notes accumulate via exit-hook append
- Source: P3, YAML comments and the paragraph "Reentrancy has a second half".
- Evidence: "a specialist's requirement is satisfied by presence, so if the field still held its last report, routing back to the same specialist would satisfy the state instantly and bounce straight to dispatch without anyone being asked."
- Evidence: "You'd see a hop in the trace and no work in it."
- Evidence: "exit appends the finding to notes, a list that only ever grows, before the edge blanks the scalar. Accumulate what you want to keep; consume what re-arms the loop."
- YAML detail: the exit action is `append: { notes: ... }`. The edge `onTransition` runs `assign: { route: "", finding: "" }`.
- Also: "three specialists share one finding field rather than owning one each - the field belongs to the hop, not to the agent".
- Layer: I
- Confidence: High.

## F3d. Stated reducer answers to the failure points
- Source: P3, "What the reducer does to the failure points"; IDX section 3.
- SPOF: "The bottleneck part is real and now visible". "Its failure mode is now 'picks the wrong value,' which is a routing-quality problem you can log, evaluate, and fix".
- Bad routing: "A malformed or out-of-vocabulary write fails coercion at the context layer - it can't silently misroute the way a hallucinated tool call can."
- Context: the per-turn injection is "the CONTEXT block (set fields, capped at 300 chars per value) plus its own objective". The supervisor's context cost is "roughly constant in the number of specialists, not the sum of their outputs."
- Specialist dependence: each specialist's objective is "templated from the staged fields... not from the supervisor's free-form paraphrase."
- Layer: M
- Confidence: High.

## F3e. Author-added failure not on the original list
- Source: P3 text (the "second half of reentrancy") and IDX bullet "Routing to the same specialist twice does nothing."
- Evidence (IDX): "Not on the original list, and the trap you'll actually hit."
- Layer: M
- Confidence: High.

---

# PATTERN 4: Hierarchical Manager / Worker

## F4. Definition, good-for, failure points (P4; matches IDX)
- Source: P4, "The pattern".
- Evidence: "Coordination is split across multiple levels rather than handled by one global supervisor."
- Good for: "Large tasks; Software-delivery organizations; Multi-domain problems; Systems with many specialized agents; Large agent organizations with scoped responsibility".
- Listed failure points: "Too many management layers; Context gets distorted as it moves through the hierarchy; Coordination becomes expensive; Recursive delegation can explode agent count".
- Motivation: one global supervisor "becomes a context black hole".
- Layer: BP
- Confidence: High.

## F4b. Key mechanism: child FSMs via spawn / alias / into
- Source: P4, "The machine" ("Three things to hold onto" list).
- Evidence: "each level of the hierarchy is its own machine, with its own context store. The director doesn't see the managers' working context - it sees a completion signal and a staged result."
- Evidence: "Each spawn creates an independent instance of the registered definition with a stable alias."
- Syntax shown: `spawn: workstream-manager, alias: api_mgr, into: results`.
- Up-channel: "When a child reaches a terminal state its context is published into the parent's results map under its alias, and that write wakes the parent". Internal bookkeeping (any `_`-prefixed field) "is stripped on the way out".
- Layer: M, I
- Confidence: High.

## F4c. Cross-level join: fsmCompleted / fsmState lookups
- Source: P4, YAML guard and the "join across levels" bullet.
- Guard: `fsmCompleted['api_mgr'] && fsmCompleted['data_mgr']`.
- Evidence: "These are lookups into tables the runtime precomputes before evaluation, not queries against a live registry, which is what keeps guards pure."
- Early guard: `fsmState['api_mgr'] != 'intake'`. Evidence: "The parent observes the children's STATE, never their values."
- Entry actions on the initial state "run at instance creation, so both children exist before the director's first turn".
- Layer: I
- Confidence: High.

## F4d. Author's stated limit: down-channel is asymmetric
- Source: P4, bullet "The down-channel is the asymmetric half".
- Evidence: "there is no declarative way to seed a child's context at spawn."
- Evidence: "The brief goes down as an ordinary addressed write - updateContext(fsmName=api_mgr, field=brief, value=...) - from the director's own turn."
- Evidence: "Aliases resolve globally, so any agent holding one can write into that instance."
- Layer: I
- Confidence: High. This is a stated limitation of the author's own DSL.

## F4e. Author's correction of prior limitation (into: removes a re-summarising agent turn)
- Source: P4, paragraph "Worth being precise about what 'reads the child's output' means now".
- Evidence: "The parent could previously see only a child's current state name and its terminal flag - never any of its values".
- Evidence: "into: removes that turn: the values arrive as data."
- Layer: I
- Confidence: High. It shows the DSL capability changed over time, so earlier descriptions of it may be stale.

## F4f. Stated reducer answers to the failure points
- Source: P4, "What the reducer does to the failure points"; IDX section 4.
- Layers: "depth is a design decision you make and can count". "adding a layer is a code review, not a runtime surprise." A level can be inlined into the parent.
- Context distortion: "Distortion is bounded to two explicit boundaries: brief in, context out." "No level re-summarizes another's working context".
- Cost: "The director's coordination cost is constant in the size of a workstream." It "parks at await and evaluates two map lookups". "the cost of a workstream is paid by the workstream."
- Agent explosion: "Two structural brakes. First, a child is a registered definition - delegation is to a known machine, not to 'spawn whatever'". Second, `maxIterations` applies at every level, and "a stuck child just keeps the parent parked, visibly, at await."
- Layer: M
- Confidence: High.

---

# PATTERN 5: Peer Handoff / Swarm

## F5. Definition, good-for, failure points (P5; matches IDX)
- Source: P5, "The pattern".
- Evidence: "Control moves dynamically between peer agents through explicit handoffs."
- Evidence: "Unlike a supervisor model, no single agent necessarily sees or controls the entire execution path".
- Good for: "Dynamic workflows; Conversational systems; Loosely coupled specialists; Tasks where the correct path is not known ahead of time; Adaptive workflows that change as information emerges".
- Listed failure points: "Infinite handoff loops; Unclear ownership; Agents bounce work around instead of completing it; Execution paths become hard to predict; Debugging and replay can be difficult".
- Layer: BP
- Confidence: High.

## F5b. Key mechanism: handoff = enum write; exit-hook append trail; stuck state; no hop counter
- Source: P5, "The machine" ("Four things to hold onto").
- Evidence: "a handoff is a context write to a typed field, and the swarm's topology is the declared edges."
- Evidence: "The chaos is real; the topology of the chaos is data."
- Evidence: "A hallucinated peer is a coercion failure, not a routing event."
- Trail: the exit hook runs `append: { trail: ... }` then `assign: { handoff: "", handoff_note: "" }`. "Doing it in exit rather than per-edge is worth noticing on its own - five edges per state, one hook."
- Evidence: "Convergence is a property of the declared graph plus the policy, not a field an agent has to keep honest."
- Hop count: "size(context.trail) is the hop count, derived from the record itself - if you wanted a hard cap of six hops, that's a guard, not a new field."
- Settings: `maxIterations: 20`, "hard backstop on the whole mesh if running headless".
- Layer: M, I
- Confidence: High.

## F5c. Author's correction: the no-progress guard does not span turns
- Source: P5, section "Infinite handoff loops" ("an earlier version of this article credited one of them with more than it does").
- Evidence: "it does not span turns, so it will not notice a swarm that bounces triage → billing → triage over three separate agent turns with the context changing each time."
- Evidence: "Cross-turn convergence is the graph and the policy, full stop."
- Real cross-turn protection: the `stuck` state plus `policy.maxIterations` ("Those two are the real cross-turn protection").
- What the guard does do: it "stops a single advance() call from oscillating between states within one microstep run".
- IDX repeats it: "often over-credited" and "does not span turns".
- Layer: M, I
- Confidence: High. This is an explicit self-correction and the author flags that an earlier version overclaimed.

## F5d. Stated reducer answers (ownership, bounce, predictability, debugging)
- Source: P5, "What the reducer does to the failure points".
- Ownership: "At any instant, the machine is in exactly one state, and that state's canTransition names the acting agent."
- Bounce: "'bounce forever' isn't a legal run." The swarm "either converges to finish, escalates to stuck, or trips the policy cap."
- Predictability: "They're still not predictable - that's the point of the pattern; the path depends on what each peer learns."
- Debugging: "the answer to 'why did the swarm go triage → billing → technical → stuck?' is literally one field."
- Layer: M
- Confidence: High.

## F5e. Author's honest trade-off: the mesh is verbose
- Source: P5, paragraph "One trade-off worth naming honestly".
- Evidence: "the mesh is verbose. Every peer state declares the same set of handoff edges, and the stuck escape repeats on each."
- Evidence: "If you find yourself copying a state five times, that's a signal the pattern might be a supervisor in disguise".
- Layer: M
- Confidence: High.

---

# PATTERN 6: Debate / Consensus (core source for the downstream debate step)

## F6. Definition, good-for, failure points (P6; matches IDX)
- Source: P6, "The pattern".
- Evidence: "Several agents examine the same problem from independent perspectives rather than dividing it into separate tasks."
- Evidence: "A judge, voting rule, scoring mechanism, or synthesis agent then determines the final outcome."
- Good for: "Ambiguous reasoning; Architecture decisions; Hypothesis evaluation; Adversarial review; Reducing individual-agent blind spots".
- Listed failure points: "Agents may share the same blind spot; Apparent consensus can reinforce a common error; Debates can run indefinitely; Judge quality becomes critical; Token cost grows rapidly".
- Author's framing: "in a group chat, convergence is a vibe". "The debate can't run indefinitely, because 'indefinitely' isn't a legal run."
- Layer: BP
- Confidence: High.

## F6b. Roles in the reference machine
- Source: P6, YAML `agents` and prose "The machine".
- Evidence: "Three debaters - a proponent, an opponent, and a pragmatist - each argue from their perspective."
- YAML agents: `moderator` (`canTransition: [question]`), `pro`, `con`, `prag` (each `[positions, critique]`), `judge` (`[judge, finalize]`).
- Example debate question in the article: "should we move the billing system to a microservices architecture?"
- The judge also writes the final decision: the `finalize` state requires `answer` from `{ agent: judge }`.
- Layer: M
- Confidence: High.

## F6c. Rounds: structure positions, then critique, then judge, repeated
- Source: P6, YAML states and bullet 1.
- States: `question`, `positions` (round 1), `critique`, `judge`, `finalize`, `done`.
- Evidence: "positions and critique each park and elicit all three debaters concurrently."
- Evidence: "The debate's rounds are structural: positions → critique → judge, repeated."
- Loop-back goes judge to critique, not back to positions. Round 2 critiques are "against the original positions rather than a paraphrase of them".
- Layer: M
- Confidence: High.

## F6d. Join: the requirement set, no counting guard
- Source: P6, bullet 1 and YAML comment.
- Evidence: "Three requirements on one field are therefore an exact all-of join with no guard at all, and nobody can file a position under a colleague's name."
- YAML comment: "Entries are keyed by their actual author, so the three requirements ARE the join".
- Fields: `positions: { type: map, of: string }` and `critiques: { type: map, of: string }`.
- Contrast with F2c: here the requirements are NOT `required: false`, because the join is all-of.
- Layer: I
- Confidence: High.

## F6e. Judge and enum verdict
- Source: P6, YAML `judge` state and the failure-point paragraph "Judge quality becomes critical".
- Verdict enum: `verdict: { type: enum, values: [consensus, continue] }`. The judge also writes `judge_note` (optional, "What's still contested (if continue)").
- Evidence: "The judge can only write verdict (an enum) and judge_note (a string). It can't declare victory by calling a tool, can't skip the finalize step, and can't route the debate anywhere other than finalize or critique."
- Evidence: "A weak judge produces a weak judge_note, which produces weak re-critiques - but that failure is legible in the store, and you can swap the judge agent (a different model, a different prompt) without touching the machine."
- Routing guards: `verdict == 'consensus'` to `finalize`; `verdict == 'continue'` to `critique`.
- Layer: M, I
- Confidence: High.

## F6f. maxIterations and the bound on indefinite debate
- Source: P6, YAML `policy` and failure-point paragraph "Debates can run indefinitely".
- YAML: `maxIterations: 8` with the comment "hard cap on the debate loop (headless)". Other policy: `maxExchangesPerStage: 30`, `maxExchangesPerAgentPerStage: 15`, `checkpointEvery: 5`.
- Evidence: "The judge's verdict is an enum with exactly two values (consensus ends the debate, continue costs a round), and policy.maxIterations caps the total exchange count, so even a judge that keeps saying continue hits the cap."
- The bound is "two things rather than the three an earlier version of this article claimed".
- Layer: M, I
- Confidence: High.

## F6g. Author's correction: no-progress guard is not a third bound
- Source: P6, paragraph "Debates can run indefinitely".
- Evidence: "it doesn't span turns, so it won't notice a debate going four rounds with slightly different words each time. Cross-round convergence is the enum and the policy."
- Layer: M
- Confidence: High. This matches the same correction in P5 and IDX.

## F6h. What is kept vs cleared each round
- Source: P6, YAML comments and bullets 2-3.
- Kept: `positions` ("opening positions, keyed by debater - kept") and `judge_note`.
- Cleared on `continue`: `critiques` (via `remove: { critiques: "*" }`) and `verdict` (via `assign: { verdict: "" }`).
- Evidence: "Positions accumulate; critiques churn. The split is the design."
- Evidence: "Emptying the map is not cosmetic: an entry that survived the round would satisfy its debater's requirement immediately, and round 2 would run without anyone being asked - a round in the trace with no argument in it."
- Evidence: "judge_note is deliberately kept, because the critique state's objective is templated on it, so the debaters come back and address the specific contested point instead of re-litigating from scratch."
- Parallel noted by the author: the same accumulate-vs-consume split as the blackboard pattern's facts and hypotheses.
- Layer: I
- Confidence: High.

## F6i. The "loop that runs and doesn't debate" inverse failure
- Source: P6, paragraph "The inverse failure is the sneakier one".
- Evidence: "If critiques weren't emptied on the loop-back, round 2's requirements would be satisfied by round 1's entries, the state would fall straight through, and the judge would grade the same critiques again."
- Evidence: "it looks like success: a loop that runs and doesn't debate. ... Rounds in the trace, no argument in them."
- Layer: M
- Confidence: High.

## F6j. Token-cost formula and why context cost stays flat
- Source: P6, paragraph "Token cost grows rapidly"; also IDX section 6.
- Evidence: "The debate's cost is exactly: (debaters × rounds × positions+critiques) + judge calls."
- Evidence: "because each debater's objective is templated from staged fields (positions, the judge's note) rather than a growing chat transcript, each round's context cost is roughly constant".
- Evidence: "A group-chat debate's prompt grows with every message; this one grows with the number of fields, which is bounded by the design."
- Caveat (gatherer observation): the author's formula uses "exactly" but does not define how positions and critiques are counted. The article gives no measured token numbers. Confidence in the formula's precision is Medium.
- Layer: BP, M
- Confidence: High that it is stated. Medium for the claim of exactness (self-reported, unmeasured).

## F6k. Author's stated limits: blind spots and false consensus
- Source: P6, failure-point paragraphs 1-2.
- Evidence: "The reducer doesn't fix this - no orchestration pattern does. Three models from the same family share the same blind spots".
- Evidence: "The fix is model diversity in the agent roster (different families, different temperatures, different evidence), not a different loop shape."
- Evidence: "the machine can't tell you the consensus is right, but it can tell you how it formed."
- Evidence: "The judge_note field is the tell: an empty note at consensus means the judge saw no live contention; a long note means the debate did work."
- Audit capability claimed: it "can tell you which debater has been agreeing with everyone for four rounds".
- Layer: BP, M
- Confidence: High.

## F6l. Walk-through detail useful for the debate step (one round)
- Source: P6, section "Walking one round of the loop".
- Evidence: "Because the map is empty, all three requirements are unsatisfied again and all three debaters are genuinely re-summoned - with the judge's note in their objective."
- Example judge_note given: "con's cost objection relies on pre-negotiated pricing; address the updated vendor quote".
- Layer: I
- Confidence: High. The example is illustrative, not empirical.

---

# PATTERN 7: Blackboard / Shared-State

## F7. Definition, good-for, failure points (P7; matches IDX)
- Source: P7, "The pattern".
- Evidence: "Agents coordinate through a shared workspace rather than through tightly controlled point-to-point handoffs."
- Evidence: "The blackboard becomes the durable coordination surface, allowing agents to operate asynchronously and with relatively loose coupling."
- Good for: "Long-running tasks; Asynchronous agents; Complex problem solving; Shared memory; Multi-agent planning; Systems where agents may join or leave dynamically".
- Listed failure points: "A bad write can contaminate every agent; Concurrent updates can conflict; Stale reads cause incorrect decisions; Provenance becomes essential; Shared state can become an uncontrolled context dump".
- Layer: BP
- Confidence: High.

## F7b. Mapping: store = board, agents = knowledge sources, reducer loop = controller
- Source: P7, "A little background".
- Evidence: "The context store is the blackboard. The agents are the knowledge sources. And the microstep reducer loop ... is the controller."
- Evidence: "It's the same architecture, with the controller made structural."
- Layer: M
- Confidence: High for the stated mapping. It is the author's self-description.

## F7c. Key mechanism: facts (list, accumulate) vs hypotheses (map, consume); the round trigger must be the map
- Source: P7, YAML comments and bullets 2-3.
- Fields: `facts: list of string` (the board, "accumulates, never cleared") and `hypotheses: map of string` ("keyed by author, cleared each round"). `status` enum is `[solved, continue, escalate]`.
- Evidence: "Facts persist, hypotheses churn. That's exactly what a blackboard is for."
- Evidence: "A list field is present from its first append onward and never goes back to absent, so it can't distinguish 'contributed this round' from 'contributed at some point.'"
- Evidence: "the board is the thing that accumulates, and the map is the thing that re-arms."
- `facts` requirements are `required: false` ("a round where an analyst learns nothing new is a legitimate round").
- Evidence: "The join is the requirement set, not a guard." Contrast with F2c, where the guard is the join.
- Settings: `maxIterations: 10`, "bounds the investigate/assess rounds (headless)".
- Layer: I
- Confidence: High.

## F7d. Stated reducer answers (contamination, concurrency, staleness, provenance, dump)
- Source: P7, "What the reducer does to the failure points"; IDX section 7.
- Contamination: "A bad write is still a bad write, but it's a typed, validated, provenanced bad write". A fact field "can carry a validate predicate". Big artifacts are `blob` and claim-checked.
- Concurrency: "the store has a single writer at a time - the reducer. The analysts run concurrently, but their writes are ordered by the machine." And "'Concurrent updates' is a concurrency problem; here it's a queue."
- Stale reads: "the ordering is: write → reducer re-runs → next agent sees the updated store."
- Provenance: "it's recorded from the actual writer, not from what the author declared." The runtime "rejects an explicit key on a field that a named requirement is gathering". `hypotheses['agent:db_expert']` "is a lookup, not a log grep".
- Join trust: "'Wait for three hypotheses' and 'wait for these three agents' hypotheses' are different claims, and only the second one survives an agent answering twice."
- Context dump: the `context:` block declares every field, `of:` types elements, and "An agent can't write a field that isn't declared". The CONTEXT block is capped (300 chars per value on the first pass, 80 on the budget pass). `_`-prefixed fields are internal.
- Layer: M, I
- Confidence: High.

## F7e. Author's stated limits and honest trade-offs
- Source: P7, last two paragraphs of "What the reducer does to the failure points".
- Evidence: "Collections are exactly one level deep, deliberately: of: names a scalar type. You can have a list of strings; you can't have a list of records."
- Workaround offered: "a map keyed by author, or a blob field holding the structure and a claim reference on the board."
- Evidence: "a classic blackboard is open-ended ... The Aegis version is a structured blackboard: the schema is fixed at authoring time, and the controller is a declared machine."
- Evidence: "The other thing you give up is opportunistic scheduling ... investigate fans out to the same three analysts every round."
- Content-driven activation "is expressible ... but it's a routing decision you author, not one the controller derives."
- Layer: M
- Confidence: High.

---

## Cross-link observations (recorded for verification, not synthesised)
- The "no-progress guard does not span turns" correction appears in P5, P6 and IDX (IDX patterns 5, 6 and 8). All state it as a correction of an earlier version of the articles. Not tested here against the runtime.
- P2 (guard is the join, `required: false`) and P6/P7 (requirement set is the join, requirements not optional) give two stated join styles. The author presents these as consistent: all-of joins on a writer-keyed map need no guard.
- P3's `finding` (scalar, consumed) plus `notes` (list, accumulated) is the same split as P5's `handoff_note` plus `trail` and P6's `critiques` plus `positions`. This is the author's "Accumulate what you keep; consume what re-arms" idiom (F0c).
- All implementation claims concern Brad Murry's own "Aegis FSM" runtime. No independent verification of its behaviour exists in these files. The cross-source verification phase should treat runtime claims as single-source (at most Medium for behaviour, High only for "the author states X").

## Declarative Conclusions
Not applicable (external written sources, not local human sources).

## Rejected Information

| # | Information | Source | Rejection Reason | Re-include If |
|---|---|---|---|---|
| 1 | Patterns 8-13 (Planner/Executor, Generator/Critic/Refiner, Auction/Contract-Net, Event-Driven/PubSub, Quorum/Voting, Dynamic Team Formation) in IDX | IDX | Outside assigned scope (patterns 1-7 only); other instances cover them | The downstream phase needs Quorum/Voting or Generator/Critic as debate alternatives |
| 2 | Full YAML bodies of the FSMs (state-by-state) beyond the quoted mechanism lines | P1-P7 | Mechanisms are captured as findings; full YAML is retrievable from the raw files | A reader needs exact DSL syntax |
| 3 | Per-article "The whole thing, one idea" walk-through tables | P1-P7 | Restate findings already captured | Never expected |
| 4 | Series intro link ("why the reactive reducer, and the two ways to run it") | IDX | A link only; the content is not in the files | The intro article is later supplied |
| 5 | P5 omitted `technical` and `security` YAML states | P5 | Editorial omission by the user; nothing gatherable | The full P5 YAML is supplied |
| 6 | Mutiny / Flink compilation claim | IDX | Mentioned only in one sentence; belongs to other categories (data/infra) | The scale-out question becomes in scope |

## Actor Relevance
Not applicable. The plan lists no actors.
