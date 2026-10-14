# Findings: bm-data-infra-ops

Task: `docs/research/2026-10-14-agent-patterns-bradmurry`
Category: bm-data-infra-ops (8 URLs: index + 7 posts)
Method: one WebFetch call per URL. The extraction prompt asked for verbatim quotes with the section heading each appears under.

**Caveat, applies to every finding below:** WebFetch returns text processed by a small model. Quotes are reproduced as returned. They were not compared byte-for-byte against page HTML. Where a page had no Patterns/Concepts/Skills/etc. headings, the grouping was assigned by the fetch step, so the grouping is not the author's. All findings are single-source, so confidence is capped at Medium until the verification phase (plan methodology). Layer tags (Big Picture / Modeling / Implementation) were assigned by this gatherer.

## Fetch Log and Gaps

| # | URL | Status | Notes |
|---|---|---|---|
| 0 | https://bradmurry.com/software/ | Fetched OK | Lists 13 posts (see index cross-check below). Contains only titles, dates and thumbnails; no description of post content. Used only to confirm the post list. |
| 1 | https://bradmurry.com/software/sparkctl/ | Fetched OK | Quotes extracted in 5 groups. No Skills-level claim beyond the wizard and the one-line recipe difference. |
| 2 | https://bradmurry.com/software/dsl-ai-partition/ | Fetched OK | Skills group returned "No direct claims about skills were found on the page." |
| 3 | https://bradmurry.com/software/ai-annotations/ | Fetched OK | Page dated Jan 11, 2026 (per fetch output). Skills group returned "No content on this topic." |
| 4 | https://bradmurry.com/software/pyflink-intro/ | Fetched OK | Mostly motivational text, few concrete technical specifics. |
| 5 | https://bradmurry.com/software/apis-at-massive-scale/ | Fetched OK | No Skills or explicit Production-guidance headings; closest content mapped by fetch step. |
| 6 | https://bradmurry.com/software/dev-ops-contract/ | Fetched OK | Skills group: "No direct match." Mapped content is about team ownership, not skills. |
| 7 | https://bradmurry.com/software/config-driven-transforms/ | Fetched OK | Post is titled "(pt1)". Part 2 is not in the index and was not fetched. |

**Gaps:**
- **Skills layer is thin or empty** for dsl-ai-partition, ai-annotations, apis-at-massive-scale and dev-ops-contract. Only sparkctl, config-driven-transforms and pyflink-intro yielded Skills quotes.
- **Truncated enumerations.** dsl-ai-partition: "But the DSL runtime ensures that no matter what the AI generates:" and "Make DSL testing trivial:" end in a colon, and their list items were not returned. dev-ops-contract: the first quote ends without a full stop, so the sentence may continue beyond the returned text.
- **Cross-links** between posts were not extracted by the prompts used. None are recorded.
- **Later series parts not in the index:** "Config Driven Data Transforms (pt1)" and "PyFlink Series (Intro)" imply parts not listed. They were not followed (outside the 18-URL list).

**Index cross-check (13 posts, confirmed from the index page):**
1. Agents in Production (Intro), `/software/agents-in-production-intro/`. Belongs to bm-agents-fsm.
2. The Agent That Builds Its Own Tools, `/software/self-evolving-agents-intro/`. Belongs to bm-agents-fsm.
3. The 13 Multi-Agent Orchestration Patterns, Implemented via the Reactive Reducer, `/software/reactive-reducer-patterns/`. Belongs to bm-agents-fsm.
4. The Reactive Reducer Pattern Series (Intro), `/software/reactive-reducer-patterns-intro/`. Belongs to bm-agents-fsm.
5. Walking a Reentrant Human-in-the-Loop FSM, Step by Step, `/software/walking-a-reentrant-human-in-the-loop-fsm-step-by-step/`. Belongs to bm-agents-fsm.
6. Binding LLMs to Finite State Machines Using the Reactive Reducer Pattern, `/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/`. Belongs to bm-agents-fsm.
7. sparkctl: Config Driven Model Serving for DGX Spark, `/software/sparkctl/`. Covered here.
8. Safe(r) Deterministic AI Code Generation Through DSL Partitioning, `/software/dsl-ai-partition/`. Covered here.
9. AI Annotations: Embedding Context in Your Codebase, `/software/ai-annotations/`. Covered here.
10. API Architecture for Massive Scale, `/software/apis-at-massive-scale/`. Covered here.
11. PyFlink Series (Intro), `/software/pyflink-intro/`. Covered here.
12. Devops Contract, `/software/dev-ops-contract/`. Covered here.
13. Config Driven Data Transforms (pt1), `/software/config-driven-transforms/`. Covered here.

Items 1 to 6 are the bm-agents-fsm category. They are listed only so the index is fully accounted for. Nothing from them was gathered here.

---

## Source: sparkctl (Implementation)

## S1. Configuration is the source of truth
- Layer: Implementation
- Source: https://bradmurry.com/software/sparkctl/, section "Patterns" / "The shape of the thing": "configuration is the source of truth."
- Evidence: "configuration is the source of truth."
- Confidence: Medium. Explicit author statement. Single source, fetch-processed quote.

## S2. CLI reconciles deployed reality to the config
- Layer: Implementation
- Source: https://bradmurry.com/software/sparkctl/, section "Concepts" / "The shape of the thing": "the CLI makes reality match the config."
- Evidence: "the CLI makes reality match the config."
- Confidence: Medium. Explicit author statement. Single source.

## S3. Command grammar copied from kubectl
- Layer: Implementation
- Source: https://bradmurry.com/software/sparkctl/, section "Patterns" / "The shape of the thing": "If a command feels familiar, that’s on purpose, the grammar is lifted straight from kubectl."
- Evidence: as quoted.
- Confidence: Medium. Explicit design statement. Single source.

## S4. Route table generated from what is deployed; same name gives a load-balanced pool
- Layer: Implementation
- Source: https://bradmurry.com/software/sparkctl/, section "Patterns" / "One endpoint for everything": "If two nodes serve the same `served_name`, they automatically become a load balanced pool."
- Supporting quote, section "Concepts" / "One endpoint for everything": "generates the route table from whatever is currently deployed"
- Evidence: as quoted.
- Confidence: Medium. Explicit behaviour claim. Single source.

## S5. One process, one port, no container runtime required
- Layer: Implementation
- Source: https://bradmurry.com/software/sparkctl/, section "Architecture" / "One endpoint for everything": "One process, one port, no docker required."
- Evidence: as quoted.
- Confidence: Medium. Explicit architecture claim. Single source.

## S6. Execution layer is a plugin behind a small interface
- Layer: Implementation
- Source: https://bradmurry.com/software/sparkctl/, section "Architecture" / "Beyond docker": "talks to a small interface rather than to docker directly."
- Supporting quote, section "Concepts" / "Beyond docker": "the execution layer is deliberately a plugin."
- Evidence: as quoted.
- Confidence: Medium. Explicit design claim. Single source.

## S7. Cluster-spanning service declared by omitting node pin
- Layer: Implementation
- Source: https://bradmurry.com/software/sparkctl/, section "Architecture" / "Splitting a model across nodes": "`tensor: 2` with no node pin tells sparkctl this service spans the cluster."
- Evidence: as quoted.
- Confidence: Medium. Explicit configuration semantics. Single source.

## S8. Split vs. replicas is a one-line recipe change
- Layer: Implementation
- Source: https://bradmurry.com/software/sparkctl/, section "Skills" / "Splitting a model across nodes": "Big split model or two fast replicas is a one line difference in a recipe"
- Evidence: as quoted (the phrasing is as returned by the fetch step).
- Confidence: Medium. Explicit claim, but the Skills label is the fetch step's grouping.

## S9. Wizard recognises model family and fills in recipe
- Layer: Implementation
- Source: https://bradmurry.com/software/sparkctl/, section "Skills" / "You don’t have to write the yaml by hand": "The wizard recognizes the model family and fills in the parts"
- Evidence: as quoted.
- Confidence: Medium. Explicit feature claim. Single source. Not verified that the wizard works as described.

## S10. CLI runs from a laptop and resolves context by hostname
- Layer: Implementation
- Source: https://bradmurry.com/software/sparkctl/, section "Configuration": "The CLI runs from your laptop and figures out context by hostname."
- Evidence: as quoted.
- Confidence: Medium. Explicit claim. Single source.

## S11. Mutating commands push the repo to nodes before running on the head node
- Layer: Implementation
- Source: https://bradmurry.com/software/sparkctl/, section "Operations" / "The shape of the thing": "Mutating commands push the repo to the nodes first and then run on the head."
- Evidence: as quoted.
- Confidence: Medium. Explicit operational claim. Single source.

## S12. Model switching is one command each way
- Layer: Implementation
- Source: https://bradmurry.com/software/sparkctl/, section "Operations" / "Single node: from zero to serving": "Switching models for a benchmark run is one command, and switching back is one command."
- Evidence: as quoted.
- Confidence: Medium. Explicit operational claim. Single source.

## S13. Deploy manifests with recipe hash, and drift detection
- Layer: Implementation
- Source: https://bradmurry.com/software/sparkctl/, section "Operations" / "Single node: from zero to serving": "every deploy writes a manifest with the recipe’s sha256 to each node."
- Supporting quote, same section: "It tells you “matches current” or “DRIFT”, not just “a container exists”."
- Evidence: as quoted.
- Confidence: Medium. Explicit operational claim. Single source.

## S14. Boot daemon serves the current recipe after power loss
- Layer: Implementation
- Source: https://bradmurry.com/software/sparkctl/, section "Production guidance" / "Single node: from zero to serving": "The boot daemon serves whatever recipe is current, so a power blip doesn’t cost you a morning."
- Evidence: as quoted.
- Confidence: Medium. Explicit reliability claim. Single source. No test evidence on page.

## S15. Corrupt shard cannot reach a serve
- Layer: Implementation
- Source: https://bradmurry.com/software/sparkctl/, section "Production guidance" / "Splitting a model across nodes": "A corrupt shard cannot reach a serve." and "That property is not negotiable in this tool."
- Evidence: as quoted.
- Confidence: Medium. Explicit reliability claim. Single source. The mechanism is not described in the returned text.

---

## Source: dsl-ai-partition (Modeling)

## D1. AI can only use pre-vetted DSL primitives
- Layer: Modeling
- Source: https://bradmurry.com/software/dsl-ai-partition/, section "Concepts" / "A Solution: Partition Generation with a DSL": "AI can only express operations the DSL supports, and the DSL only exposes safe, pre-vetted primitives."
- Evidence: as quoted.
- Confidence: Medium. Explicit design claim. Single source.

## D2. Determinism through constraint
- Layer: Modeling
- Source: https://bradmurry.com/software/dsl-ai-partition/, section "Concepts" / "Determinism Through Constraint": "Same requirements, same DSL output."
- Supporting quote, section "Patterns" / "Determinism Through Constraint": "Limited vocabulary. Enforced structure. Single canonical representation."
- Evidence: as quoted.
- Confidence: Medium. Explicit claim. Single source. Determinism is asserted, not measured on the page.

## D3. Motivation: LLM code output is non-deterministic
- Layer: Big Picture
- Source: https://bradmurry.com/software/dsl-ai-partition/, section "Concepts" / "Intro": "Ask an LLM to implement the same requirements twice and you’ll get completely different code."
- Evidence: as quoted.
- Confidence: Medium. Explicit problem statement. Single source. Anecdotal, no measurement on page.

## D4. DSL sits between AI orchestration and human-written critical code
- Layer: Modeling
- Source: https://bradmurry.com/software/dsl-ai-partition/, section "Architecture" / "A Solution: Partition Generation with a DSL": "A Domain-Specific Language sits between AI-generated orchestration and human-written critical components."
- Evidence: as quoted.
- Confidence: Medium. Explicit architecture claim. Single source.

## D5. Change routing: most changes on a fast track, a minority on a careful track
- Layer: Modeling
- Source: https://bradmurry.com/software/dsl-ai-partition/, section "Architecture" / "When to Update Core vs DSL": "The DSL constrains 90% of changes to fast track. Only 10% need the careful track."
- Supporting quote, section "Production Guidance" / "The Two-Track Model (Fast Track)": "Deploy frequently (multiple times daily)"
- Evidence: as quoted.
- Confidence: Low. The 90/10 split is stated without any data or method on the page. Recorded as the author's assertion only.

## D6. Embedded DSLs for complex logic in safe host languages
- Layer: Modeling
- Source: https://bradmurry.com/software/dsl-ai-partition/, section "Patterns" / "Implementation Patterns (Embedded DSLs for Complexity)": "For sophisticated logic, use embedded DSLs in safe languages:"
- Evidence: as quoted. The list that follows was not returned.
- Confidence: Medium. Explicit recommendation. Single source. Incomplete (the list is missing).

## D7. Declarative YAML/JSON configs with strict schemas
- Layer: Implementation
- Source: https://bradmurry.com/software/dsl-ai-partition/, section "Configuration" / "Implementation Patterns (Start with YAML/JSON)": "Declarative configs are easy to parse, validate, and version control:"
- Supporting quote, section "Configuration" / "Implementation Patterns (Schema Validation)": "Define strict schemas:"
- Evidence: as quoted. The lists that follow were not returned.
- Confidence: Medium. Explicit recommendation. Single source.

## D8. Runtime logging and resource limits
- Layer: Implementation
- Source: https://bradmurry.com/software/dsl-ai-partition/, section "Operations" / "Built-In Governance": "Every execution logged with full context"; section "Operations" / "Security Considerations (Sandbox the Runtime)": "Resource limits: CPU, memory, execution time"
- Evidence: as quoted.
- Confidence: Medium. Explicit recommendations. Single source.

## D9. DSL runtime is said to constrain whatever the AI generates (list not returned)
- Layer: Implementation
- Source: https://bradmurry.com/software/dsl-ai-partition/, section "Operations" / "Speed With Reduced Risk": "But the DSL runtime ensures that no matter what the AI generates:"
- Evidence: as quoted. The enumerated guarantees were not returned, so the guarantee itself is not confirmed.
- Confidence: Low. Truncated; the claim cannot be assessed from the extracted text.

## D10. DSL testing (list not returned)
- Layer: Implementation
- Source: https://bradmurry.com/software/dsl-ai-partition/, section "Operations" / "Implementation Patterns (Testing Infrastructure)": "Make DSL testing trivial:"
- Evidence: as quoted. The list was not returned.
- Confidence: Low. Truncated.

## D11. Start with one non-critical process
- Layer: Implementation
- Source: https://bradmurry.com/software/dsl-ai-partition/, section "Production Guidance" / "Getting Started": "Start with a single, non-critical process"
- Evidence: as quoted.
- Confidence: Medium. Explicit recommendation. Single source.

---

## Source: ai-annotations (Implementation / Modeling)

## N1. Unified syntax for semantic metadata in code and docs
- Layer: Implementation
- Source: https://bradmurry.com/software/ai-annotations/, section "Concepts" / "What Are AI Annotations?": "The Agent Annotation Standard provides a unified syntax for embedding semantic metadata in code and documentation."
- Evidence: as quoted.
- Confidence: Medium. Explicit definition. Single source. The "standard" is not shown on the page as a separate spec.

## N2. Agents get enriched context from annotated files
- Layer: Implementation
- Source: https://bradmurry.com/software/ai-annotations/, section "Concepts" / "Why AI Annotations?": "When AI agents read annotated files, they automatically receive enriched context"
- Evidence: as quoted.
- Confidence: Medium. Explicit claim. Single source. The retrieval mechanism is not described in the returned text.

## N3. Git gives an audit trail of annotation changes
- Layer: Implementation
- Source: https://bradmurry.com/software/ai-annotations/, section "Concepts" / "Why AI Annotations?": "Git naturally provides an audit trail of how annotations evolve alongside code"
- Evidence: as quoted.
- Confidence: Medium. Explicit claim. Single source.

## N4. Single-line annotations placed before code elements
- Layer: Implementation
- Source: https://bradmurry.com/software/ai-annotations/, section "Patterns" / "Two Syntax Styles > Inline Syntax": "Place a single-line annotation before any code element: functions, methods, classes, or at the top of files"
- Evidence: as quoted.
- Confidence: Medium. Explicit syntax rule. Single source.

## N5. Canonical references as an authoritative pattern
- Layer: Modeling
- Source: https://bradmurry.com/software/ai-annotations/, section "Patterns" / "Canonical References": "Establish authoritative patterns, architectures, and standards that AI agents and developers should follow"
- Supporting: section "Architecture" / "Canonical References" notes an example `repository_pattern` canon annotation (fetch step paraphrase, not a quote).
- Evidence: as quoted.
- Confidence: Medium. Explicit recommendation. Single source.

## N6. Annotation properties use Toon Format
- Layer: Implementation
- Source: https://bradmurry.com/software/ai-annotations/, section "Configuration" / "Why Toon Format?": "Properties in annotations use Toon Format, a lightweight data notation designed for readability and simplicity."
- Evidence: as quoted.
- Confidence: Medium. Explicit format choice. Single source. The format itself was not checked.

## N7. CI gates and pattern validation from annotations
- Layer: Implementation
- Source: https://bradmurry.com/software/ai-annotations/, section "Operations" / "Tooling Integration": "CI/CD: Enforce policies, gate deployments based on annotation metadata"; "Pattern validation: Tools can verify new code follows canonical examples"
- Evidence: as quoted.
- Confidence: Medium. Explicit claims about possible tooling. Single source. No shipped tool is named in the returned text.

## N8. Incremental adoption: add @!link or @!todo, configure linters and CI
- Layer: Implementation
- Source: https://bradmurry.com/software/ai-annotations/, section "Production Guidance" / "Getting Started": "Start simple: Add `@!link` or `@!todo` annotations to existing code"; "Configure linters and CI to recognize annotations"
- Evidence: as quoted.
- Confidence: Medium. Explicit recommendation. Single source.

## N9. Team style guide for patterns
- Layer: Modeling
- Source: https://bradmurry.com/software/ai-annotations/, section "Production Guidance" / "Getting Started": "Document patterns: Create a style guide for your team"
- Evidence: as quoted.
- Confidence: Medium. Explicit recommendation. Single source.

---

## Source: pyflink-intro (Implementation; Big Picture for the streaming motivation)

## P1. Stream processing is suited to ML/AI pipelines and event-driven architecture
- Layer: Big Picture
- Source: https://bradmurry.com/software/pyflink-intro/, section "Why Flink?": "Orchestrate ML/AI training and augmentation pipelines" and "Support Event Driven Architecture" (listed as reasons to adopt stream processing).
- Evidence: as quoted.
- Confidence: Medium. Explicit motivation. Single source. Introductory, not evidenced on the page.

## P2. Batch is a bounded stream
- Layer: Modeling
- Source: https://bradmurry.com/software/pyflink-intro/, section "Why Flink?": "You can batch with streaming, after all that’s just a bounded stream of batch size"
- Evidence: as quoted (wording as returned).
- Confidence: Medium. Explicit conceptual claim. Single source.

## P3. Micro-batching is not streaming
- Layer: Big Picture
- Source: https://bradmurry.com/software/pyflink-intro/, section "Why Flink?": "no matter how “micro” you make your batches in a batch-oriented system, you’ll never quite achieve streaming"
- Evidence: as quoted.
- Confidence: Medium. Explicit conceptual claim. Single source.

## P4. Reasons to pick PyFlink: Python team skill, and SQL limits
- Layer: Implementation
- Source: https://bradmurry.com/software/pyflink-intro/, section "Why (Py)Flink?": "Maybe you’re on a team of elite pythonistas." and "You may have use cases that can’t be easily resolved with Flink’s pure SQL interface"
- Evidence: as quoted.
- Confidence: Medium. Explicit but informal statements. Single source. Opinion rather than evidence.

## P5. PyFlink uses a Py4J bridge to JVM objects
- Layer: Implementation
- Source: https://bradmurry.com/software/pyflink-intro/, section "Why (Py)Flink?": "pythonic bindings to the underlying JVM constructs using Py4J"; "Using the Py4J bridge, you can directly interact with “real” Flink JVM objects from python"
- Evidence: as quoted.
- Confidence: Medium. Explicit architecture claim. Single source.

## P6. Performance caveat for very high throughput
- Layer: Implementation
- Source: https://bradmurry.com/software/pyflink-intro/, section "Production guidance" / "Why (Py)Flink?": "Some (very)high throughput use cases may have performance impacts when using PyFlink vs JVM directly"
- Evidence: as quoted. The "escape hatches" remark came back only as a paraphrase, so it is not quoted here.
- Confidence: Medium. Explicit caveat. Single source. No benchmark on the page.

## P7. Prerequisites and local-first setup, with deployment to AWS MAF as the stated goal
- Layer: Implementation
- Source: https://bradmurry.com/software/pyflink-intro/, section "Configuration" / "Setting up a job": "Make sure you have python 3.8 or greater installed as well as the `virtualenv` package."; section "Operations" / "Let's dive in!": "instead we’ll focus on getting a local setup running that can then be deployed to AWS MAF"
- Evidence: as quoted.
- Confidence: Medium. Explicit statements. Single source. "AWS MAF" is not expanded in the returned text, so it is not expanded here.

---

## Source: apis-at-massive-scale (Big Picture / Modeling / Implementation)

## A1. Request-time composition does not scale
- Layer: Big Picture
- Source: https://bradmurry.com/software/apis-at-massive-scale/, section "Patterns" / "Traditional approaches (and why they'd crash and burn)": "you're essentially doing real-time data composition on every request."
- Evidence: as quoted.
- Confidence: Medium. Explicit problem statement. Single source. No load figures on page.

## A2. Components subscribe only to events they need
- Layer: Modeling
- Source: https://bradmurry.com/software/apis-at-massive-scale/, section "Patterns" / "Transform reactively": "Each transformation component subscribes only to the specific events it cares about."
- Evidence: as quoted.
- Confidence: Medium. Explicit pattern statement. Single source.

## A3. Decoupling data change from consumption
- Layer: Modeling
- Source: https://bradmurry.com/software/apis-at-massive-scale/, section "Concepts" / "Reactive architecture to the rescue": "This approach allows data to change and flow independently of how fast (or slow) it's being consumed."; section "Concepts": "decouple data volatility from consumption patterns."
- Evidence: as quoted.
- Confidence: Medium. Explicit design principle. Single source.

## A4. Precompute on change, serve precomputed results
- Layer: Implementation
- Source: https://bradmurry.com/software/apis-at-massive-scale/, section "Concepts" / "Transform reactively": "we pre-compute every permutation once when the data changes, then serve pre-computed results at lightning speed."
- Evidence: as quoted. "Every permutation" is the author's wording; the page does not show how many permutations exist.
- Confidence: Medium. Explicit claim. Single source. Speed claim not measured on page.

## A5. Write deduplication by content hash
- Layer: Implementation
- Source: https://bradmurry.com/software/apis-at-massive-scale/, section "Architecture" / "Intelligent writes + CDC": "before writing any data, we compute a hash. If the hash matches what we wrote last time, we skip the write entirely."
- Evidence: as quoted.
- Confidence: Medium. Explicit mechanism. Single source.

## A6. Same precomputed data written to a fast compute layer backed by Redis
- Layer: Implementation
- Source: https://bradmurry.com/software/apis-at-massive-scale/, section "Architecture" / "Distribution": "the same pre-computed data to both our high-speed compute layer (backed by Redis)"
- Evidence: as quoted. The quote is partial. The "dual-write" wording is this gatherer's label, not the author's.
- Confidence: Medium. Explicit but partial. Single source. The second write target is not fully returned.

## A7. Resource URL scheme
- Layer: Implementation
- Source: https://bradmurry.com/software/apis-at-massive-scale/, section "Configuration" / "Distribution": "/{resource-category}/{resource-type}/{resource-key}"
- Evidence: as quoted.
- Confidence: Medium. Explicit scheme. Single source. The scheme is shown without surrounding rationale.

## A8. Static-file fallback when the API is down
- Layer: Implementation
- Source: https://bradmurry.com/software/apis-at-massive-scale/, section "Operations" / "Distribution": "Even if our entire API infrastructure went down, we could fall back to serving static files from S3."
- Evidence: as quoted.
- Confidence: Medium. Explicit description of a fallback. Single source. Described as possible ("could"), not as tested.

## A9. Automated edge cache invalidation on fresh data
- Layer: Implementation
- Source: https://bradmurry.com/software/apis-at-massive-scale/, section "Operations" / "Oh, one more thing": "The moment fresh data hit our system, we could automatically invalidate cached entries at the edge."
- Evidence: as quoted.
- Confidence: Medium. Explicit but hypothetical ("could"). Single source.

## A10. Caching trade-off
- Layer: Implementation
- Source: https://bradmurry.com/software/apis-at-massive-scale/, section "Production guidance" / "Traditional approaches": "Cache too aggressively, and you serve stale data."
- Evidence: as quoted.
- Confidence: Medium. Explicit trade-off. Single source.

## A11. General guidance: precompute and build redundancy
- Layer: Implementation
- Source: https://bradmurry.com/software/apis-at-massive-scale/, section "Production guidance" / "Until next time…": "Pre-compute what you can. Build redundancy at every layer."
- Evidence: as quoted.
- Confidence: Low. Generic closing guidance with no supporting argument in the returned text.

---

## Source: dev-ops-contract (Implementation / Modeling)

## O1. Apply code-quality rigour to DevOps
- Layer: Modeling
- Source: https://bradmurry.com/software/dev-ops-contract/, section "Patterns" / "Tenets of a Devops Contract": "If we applied to the practice of devops the same principals and rigor that we collectively value when writing code"
- Evidence: as quoted. The sentence is truncated in the returned text.
- Confidence: Medium. Explicit statement. Single source. Truncated.

## O2. Commands should take as few inputs as possible
- Layer: Modeling
- Source: https://bradmurry.com/software/dev-ops-contract/, section "Patterns" / "Tenets of a Devops Contract": "Commands should have as few inputs (args, etc…) as possible and none is best."
- Evidence: as quoted.
- Confidence: Medium. Explicit design tenet. Single source.

## O3. Commands represent abstract concerns
- Layer: Modeling
- Source: https://bradmurry.com/software/dev-ops-contract/, section "Concepts" / "Tenets of a Devops Contract": "Commands represent abstract concerns such as installing libraries from a package manager"
- Evidence: as quoted.
- Confidence: Medium. Explicit concept. Single source.

## O4. Multi-phase lifecycle with short-circuit errors
- Layer: Implementation
- Source: https://bradmurry.com/software/dev-ops-contract/, section "Architecture" / "Tenets of a Devops Contract": "A multi-phase build system defines a contract’s lifecycle"; "errors should immediately short-circuit the lifecycle"
- Evidence: as quoted.
- Confidence: Medium. Explicit architecture rules. Single source.

## O5. Lifecycle owns command ordering
- Layer: Implementation
- Source: https://bradmurry.com/software/dev-ops-contract/, section "Architecture" / "Tenets of a Devops Contract (lifecycle detail)": "The lifecycle understands how to run commands, and controls the order of their execution"
- Evidence: as quoted.
- Confidence: Medium. Explicit architecture claim. Single source.

## O6. Command parameters are declarative
- Layer: Implementation
- Source: https://bradmurry.com/software/dev-ops-contract/, section "Configuration" / "Tenets of a Devops Contract": "Parameters required by the inner workings of a command should be declarative"
- Evidence: as quoted.
- Confidence: Medium. Explicit rule. Single source.

## O7. Team autonomy and faster issue resolution
- Layer: Implementation
- Source: https://bradmurry.com/software/dev-ops-contract/, section "Operations" / "Benefits, Developer/Team Autonomy": "Developers can make adjustments to various aspects such as reconfiguring a test runner"; section "Operations" / "Benefits, Operational Knowledge Sharing": "they’re able to help find and resolve issues faster"
- Evidence: as quoted.
- Confidence: Low. Benefit claims with no data or example on the page.

## O8. Portability across CI/CD runtimes
- Layer: Implementation
- Source: https://bradmurry.com/software/dev-ops-contract/, section "Production guidance" / "Benefits, Reusable Infrastructure and Automation": "Moving to new CI/CD runtimes (or even supporting several simultaneously) becomes relatively trivial."
- Evidence: as quoted.
- Confidence: Low. Unsupported assertion ("relatively trivial") with no example in the returned text.

## O9. Ownership follows fixing
- Layer: Modeling
- Source: https://bradmurry.com/software/dev-ops-contract/, section "Production guidance" / "Tenets of a Devops Contract": "Commands may be shared and reused, but if you can’t fix it, you don’t own it."
- Evidence: as quoted.
- Confidence: Medium. Explicit rule. Single source.

---

## Source: config-driven-transforms (Modeling / Implementation; part 1 only)

## C1. Bespoke per-shape transform code is the problem
- Layer: Big Picture
- Source: https://bradmurry.com/software/config-driven-transforms/, section "Patterns" / "There's no money in manually manipulating data.": "too much bespoke code is created for the sole purpose of transforming one shape of data to another"
- Evidence: as quoted.
- Confidence: Medium. Explicit problem statement. Single source.

## C2. Transformations as DSL documents enable versioning
- Layer: Modeling
- Source: https://bradmurry.com/software/config-driven-transforms/, section "Concepts" / "By moving transformation into a DSL document" (Versioning): "By moving transformation into a DSL document, the same code can process multiple versions of a transformation"
- Evidence: as quoted.
- Confidence: Medium. Explicit claim. Single source.

## C3. Transform changes move from code deployment to configuration
- Layer: Implementation
- Source: https://bradmurry.com/software/config-driven-transforms/, section "Configuration" / "There's no money in manually manipulating data.": "we want to move that change from a code deployment requirement to a configuration concern"
- Evidence: as quoted.
- Confidence: Medium. Explicit goal. Single source.

## C4. Low learning curve because the DSL is data
- Layer: Modeling
- Source: https://bradmurry.com/software/config-driven-transforms/, section "Skills" / "Learning Curve": "Simple path traversal to reshape data has a low learning curve, as the DSL is represented by the data itself."
- Evidence: as quoted.
- Confidence: Medium. Explicit claim. Single source. No user study on page.

## C5. Validate input shape before transforming
- Layer: Modeling
- Source: https://bradmurry.com/software/config-driven-transforms/, section "Architecture" / "Validation": "we can make sure the section being transformed is in the right shape"
- Evidence: as quoted.
- Confidence: Medium. Explicit practice. Single source.

## C6. Post-transform schema validation is the most important check
- Layer: Implementation
- Source: https://bradmurry.com/software/config-driven-transforms/, section "Production guidance" / "Validation": "The most important schema validation is usually the Post-transform"
- Evidence: as quoted.
- Confidence: Medium. Explicit recommendation with hedge ("usually"). Single source.

## C7. Standard format for test and result data
- Layer: Implementation
- Source: https://bradmurry.com/software/config-driven-transforms/, section "Operations" / "Testing": "Providing test and result data in a standard format makes it easy for teams to test and iterate on their transforms"
- Evidence: as quoted.
- Confidence: Medium. Explicit practice. Single source.

---

## Declarative Conclusions

Not applicable. All sources are external web pages, so the local-human-source process (methodology §9) does not apply.

## Rejected Information

| # | Information | Source | Rejection Reason | Re-include If |
|---|---|---|---|---|
| R1 | Index page thumbnails and post dates | https://bradmurry.com/software/ | Presentation metadata, not content about patterns or practices | A date-based or publication-order question is asked |
| R2 | Index entries and content of the six bm-agents-fsm posts (agents-in-production-intro, self-evolving-agents-intro, reactive-reducer-patterns, reactive-reducer-patterns-intro, walking-a-reentrant-human-in-the-loop-fsm, binding-llms-to-finite-state-machines) | https://bradmurry.com/software/ (index) | Belong to another category (bm-agents-fsm), gathered by another instance. Not gathered here. | Never for this instance. Cross-reference only. |
| R3 | PyFlink setup mechanics: Python 3.8+ prerequisite details, the `make fresh` target, virtualenv activation steps (only the prerequisite and the goal are kept as P7) | https://bradmurry.com/software/pyflink-intro/, section "Setting up a job" | Tutorial steps, not patterns or production guidance | The question asks for the developer setup procedure |
| R4 | Hardware-specific framing of sparkctl (title targets DGX Spark); no hardware specifications were returned | https://bradmurry.com/software/sparkctl/ (title) | Hardware target is context for the tool, not a generalisable pattern | The question asks for hardware-specific deployment guidance |
| R5 | Later parts of "Config Driven Data Transforms" (pt1 shown in index) and "PyFlink Series" (Intro shown in index) | https://bradmurry.com/software/ (index) | Not in the 18-URL source list. Plan says not to follow links outside the list. | The source list is extended to include the later parts |
| R6 | Cross-links between posts | All fetched pages | Not extracted by the prompts used. Plan says to record for verification, but no links were returned. | Verification phase re-fetches with a link-extraction prompt |
| R7 | Truncated enumerations in dsl-ai-partition (D9, D10) | https://bradmurry.com/software/dsl-ai-partition/ | Items not returned by the fetch step. They are logged as Low-confidence findings rather than rejected. | A re-fetch returns the full lists |

## Actor Relevance

Omitted. The plan lists no actors.
