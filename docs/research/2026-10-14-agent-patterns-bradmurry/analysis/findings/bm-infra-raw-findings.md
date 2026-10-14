# Findings: bm-infra-raw

Re-gather from full-text local files in `analysis/sources-raw/`. All quotes copied verbatim from those files. Layer tags: BP = Big Picture, MOD = Modeling, IMPL = Implementation. Raw-file names abbreviated (e.g. "30-apis" = `30-apis-at-massive-scale.md`).

Provenance notes (affect confidence ceiling):
- 30, 31, 32: full article text pasted by the user 2026-10-14 (raw, as pasted).
- 42, 43, 44, 45: curl HTTP 200 fetch 2026-10-14, machine-extracted from HTML (tables flattened; inline formatting lost). Quoted text is the extracted text.
- Single-source claims: at most Medium per plan until the verification phase; "High" below means the quote is explicit about the stated claim (confidence in what the author says, not that it is independently verified).

---
# PART A. apis-at-massive-scale
URL: https://bradmurry.com/software/apis-at-massive-scale/ (file `30-apis-at-massive-scale.md`; page heading "API Architecture for Massive Scale")

## A1. Context and requirements (CNN 2020 election)
- Layer: BP
- Source: https://bradmurry.com/software/apis-at-massive-scale/ , 30-apis
- Evidence: "High write throughput: Vote data could update every second during live counting, then sit dormant for days or months during the officiating process (and all those recounts…)"
- Further requirements listed: "Extreme read throughput: We're talking billions of requests served"; "Multiple data views: Different audiences needed different transformations and aggregations of the same underlying data"; "100% uptime: Failure was simply not an option". Time constraint: "I had less than two months to design, build, and deploy the entire system before Election Day."
- Confidence: High - explicit list in "Design considerations".

## A2. Why traditional request-time composition fails
- Layer: BP
- Source: same, section "Traditional approaches"
- Evidence: "you're essentially doing real-time data composition on every request."
- Listed problems: data layer bottlenecks (provisioning read/write nodes, deadlocking writes, replication lag, "Why write data that hasn't even changed?"); expensive access patterns ("You're recomputing the same transformations over and over, even when the underlying data is identical"; URL structure "becomes a tangled mess"); disaster recovery ("How many hot/hot stacks do you need across multiple regions to guarantee uptime?").
- Confidence: High.

## A3. Why caching is rejected as the answer
- Layer: BP
- Source: same, section "Traditional approaches" (caching illustration)
- Evidence: "caching is a balancing act. Cache too aggressively, and you serve stale data. Cache too conservatively, and you're not solving the problem."
- Context: "Data integrity wasn't just important; it was everything."
- Confidence: High.

## A4. Why microservices / GraphQL are rejected as the answer
- Layer: BP
- Source: same
- Evidence: "microservices don't solve our fundamental scale and disaster recovery challenges. They just distribute the problem across more services."
- GraphQL: "And GraphQL? It helps with URL sprawl, but the only fallback for GraphQL going down… is more GraphQL."
- Acknowledged benefit: microservices "do help with data hotspots" (text: "The microservice pattern does help with data hotspots.").
- Confidence: High.

## A5. Core principle: decouple data volatility from consumption (event-driven / Flow Based Programming)
- Layer: BP / MOD
- Source: same, "Reactive architecture to the rescue"
- Evidence: "I took event-driven thinking a step further by implementing Flow Based Programming. This approach allows data to change and flow independently of how fast (or slow) it's being consumed."
- Key insight quote: "decouple data volatility from consumption patterns."
- Also: "I've noticed a pattern: event-driven architecture almost always wins." (author's experience claim, unsupported by data in the post)
- Structure: "three loosely-coupled components": intelligent writes + CDC; transform reactively; distribution.
- Confidence: High (explicit); the "almost always wins" claim is an assertion without evidence.

## A6. Hash-before-write (intelligent writes) keeping CDC stream clean
- Layer: IMPL / MOD
- Source: same, "Intelligent writes + CDC"
- Evidence: "before writing any data, we compute a hash. If the hash matches what we wrote last time, we skip the write entirely. No unnecessary I/O, no wasteful downstream processing."
- Effect: "This keeps our Change Data Capture (CDC) output stream clean: it only contains actual changes."
- Semantics: "You get closer to \"exactly once\" processing semantics, though that can introduce its own challenges around data freshness that I'll save for another article."
- Sources of events: "These might come from API polling, message brokers, or batch processes."
- Confidence: High. Caveat: the data-freshness challenge is stated but not elaborated in this post.

## A7. Reactive transform: compute on change, not on request
- Layer: MOD / IMPL
- Source: same, "Transform reactively"
- Evidence: "we pre-compute every permutation once when the data changes, then serve pre-computed results at lightning speed."
- Contrast: "In the traditional design, you compute transformations and aggregations during the request/response cycle, potentially thousands of times per second, even when the underlying data hasn't changed."
- Result: "This completely decouples write patterns from read requirements."
- Confidence: High.

## A8. Content-based router pattern for transform and distribution subscriptions
- Layer: MOD
- Source: same, "Transform reactively" and "Distribution"
- Evidence (transform): "The secret sauce is the content-based router pattern. Each transformation component subscribes only to the specific events it cares about."
- Example given: "Want a dataset showing only Senate races where a woman is leading? Your component can filter for exactly that and ignore everything else."
- Chaining/extension: "transformations can produce new events that trigger other transformations, creating powerful data processing pipelines. And because each transformation is independent, you can add new data views without touching existing ones."
- Evidence (distribution): "By letting each API stack subscribe to specific datasets via content-based routing, every team can scale independently while sharing the same underlying data stream."
- Motivation (distribution): "different business units often need the same data but must remain isolated in separate cloud accounts for security, compliance, or billing reasons."
- Confidence: High.

## A9. Pre-computed paths: API routing as file paths
- Layer: IMPL
- Source: same, "Distribution"
- Evidence: "Since we've pre-computed every permutation, our API routing becomes beautifully simple: /{resource-category}/{resource-type}/{resource-key}"
- Examples quoted by the post: `/weather/humidity/US.json`, `/weather/humidity/US-GA.json`, `/covid19/reported-cases/NYC.json`, `/results/county-races/2022-AG-CO.json`.
- Observation: "These are just file paths."
- Confidence: High.

## A10. Dual-write to Redis compute layer and S3 as DR fallback
- Layer: IMPL / BP
- Source: same, "Distribution"
- Evidence: "we can write the same pre-computed data to both our high-speed compute layer (backed by Redis) and to object storage like AWS S3."
- Fallback: "Even if our entire API infrastructure went down, we could fall back to serving static files from S3."
- Trade-off stated: "S3's eventual consistency might mean the data is a few seconds stale compared to our compute layer, but we'd still be serving accurate election data to the world."
- Framing: "This dual-write strategy was my sleep-at-night insurance policy." and "That redundancy was non-negotiable."
- Confidence: High.

## A11. Reactive notifications / CDN invalidation as by-product
- Layer: IMPL
- Source: same, "Oh, one more thing"
- Evidence: "reactive notifications to external systems when data changes."
- Use: "The moment fresh data hit our system, we could automatically invalidate cached entries at the edge. No stale data, no manual intervention."
- Other suggested uses (hypothetical): "triggering Slack notifications when specific conditions are met, firing webhooks for downstream systems, kicking off automated testing when certain thresholds are exceeded."
- Confidence: High (CDN invalidation described as used; others are "You can imagine" suggestions, not claimed as done).

## A12. Outcome numbers as stated
- Layer: BP
- Source: same, "The outcome"
- Evidence: "We served billions of requests with accurate, real-time election data to a global audience. CNN recorded two of the highest traffic days in its 40+ year history, and our architecture didn't even break a sweat."
- Also: "The system held."
- Note: no precise req/s, latency, or uptime figures are given; "billions of requests" and "two of the highest traffic days" are the only quantification.
- Confidence: High that this is what is stated; claim itself is self-reported, uncorroborated.

## A13. Closing prescriptions
- Layer: BP
- Source: same, "Until next time…"
- Evidence: "Decouple your data changes from your consumption patterns. Pre-compute what you can. Build redundancy at every layer."
- Also: "sometimes the best solution isn't the one everyone else is using; it's the one that fits your specific constraints."
- Confidence: High.

Cross-links/dependencies recorded: none to other of the 18 URLs inside this post (post mentions "another article" on data freshness, unnamed). Post does not mention AI/LLM agents at all.

---
# PART B. devops-contract
URL: https://bradmurry.com/software/dev-ops-contract/ (file `31-devops-contract.md`; page heading "Devops Contract")

## B1. Problem statement: coupling between software and ops automation
- Layer: BP
- Source: https://bradmurry.com/software/dev-ops-contract/ , 31-devops
- Evidence: "That quality is an intimate understanding of a project and its artifacts by the automation and infrastructure that is responsible for making it operational."
- Question posed: "How can we avoid this tight coupling between the software we develop and the scripts and configurations operations requires to develop it?"
- Confidence: High.

## B2. Analogy to other contracts; definition of "Devops Contract"
- Layer: BP / MOD
- Source: same
- Evidence: "Defining and agreeing to these contracts allows us to interact across teams, avoid tight couplings, and adapt our systems over time in a reliable way."
- Definition: "We refer to the solution to this gap as a \"Devops Contract\""
- Principle: "If we applied to the practice of devops the same principals and rigor that we collectively value when writing code - such as encapsulation, separation of concerns, dryness, etc… we'd end up with something we can refer to as a devops contract."
- Confidence: High.

## B3. Two components: command interface + lifecycle
- Layer: MOD
- Source: same, "Tenets of a Devops Contract"
- Evidence: "The primary components of a devops contract are an interface of commands + a lifecycle that knows when to run those commands:"
- Confidence: High.

## B4. Lifecycle = multi-phase build system, phases, short-circuit on error
- Layer: MOD / IMPL
- Source: same
- Evidence: "Phases represent concepts such as initializing the environment, resolving external dependencies, performing checks on the source artifacts, building/deploying, and cleaning things up."
- Error semantics: "As commands are executed within each phase, errors should immediately short-circuit the lifecycle."
- Examples of systems: "AWS Codebuild/Codepipeline, Jenkins, and GitHub Actions are examples of multi-phased build systems."
- Confidence: High.

## B5. Command interface: abstract concerns, black-box to lifecycle
- Layer: MOD
- Source: same
- Evidence (commands): "Commands represent abstract concerns such as installing libraries from a package manager, running lints and unit tests, building artifacts, deploying a stack, integration testing and removing a stack"
- Evidence (opacity): "The lifecycle understands how to run commands, and controls the order of their execution, but should have no knowledge or dependencies on what happens inside a command."
- Evidence (inputs): "Commands should have as few inputs (args, etc…) as possible and none is best."
- Evidence (parameters): "Parameters required by the inner workings of a command should be declarative, using environmental variables is a good way to achieve this."
- Confidence: High.

## B6. Ownership rules
- Layer: MOD / BP
- Source: same
- Evidence: "Commands should be understood, configurable (and typically implemented) by the team that develops the software."
- Evidence: "Commands may be shared and reused, but if you can't fix it, you don't own it."
- Confidence: High.

## B7. Benefits (three areas)
- Layer: BP
- Source: same, "Benefits"
- Reusable infrastructure: "It becomes possible to develop generic pipelines that are composed of reusable components in configurations that best support the software's needs."; "Moving to new CI/CD runtimes (or even supporting several simultaneously) becomes relatively trivial."; "Increased adoption of devops best practices by reducing the amount of ceremony and integration around standing up and configuring pipelines."
- Team autonomy: "Developers can make adjustments to various aspects such as reconfiguring a test runner or adding code transpiler without the need to redeploy a pipeline."; "Potential for innovation is increased by empowering teams to explore new infrastructure and related tooling."
- Knowledge sharing: "As teams become more responsible for defining how their software is tested and deployed, they're able to help find and resolve issues faster and make more informed choices in their architecture."
- Confidence: High (claims stated; no evidence or metrics provided).

## B8. Concrete phase-to-command mapping (Makefile)
- Layer: IMPL
- Source: same, "Example code is worth a 1000 lines of markdown"
- Evidence: "Init: runs make deps"; "Build: runs make test followed by make build if the tests are successful"; "Deploy: runs make deploy"
- Interface: Makefile targets `deps`, `test`, `build`, `deploy` (each `.PHONY`, echo stubs in the base interface).
- Implementations shown for: static web app on S3 (`npm i`, `jest  --coverage`, `webpack -p`, `aws --region $AWS_REGION s3 sync dist $AWS_S3_BUCKET --delete`), NPM library (`tsc`, `semantic-release`), Python package (`pip install -r requirements.txt`, `python -m unittest discover -s tests`, `python setup.py bdist_wheel`, `twine upload ...`), serverless app (`sls deploy --stage=$STAGE --account=$ACCOUNT`).
- Conclusion quote: "It's immediately obvious that the same pipeline, one that is soley concerned with issuing commands in a pre-defined order can be used for all sorts of applications."
- Confidence: High.

Cross-links: none. Post does not mention AI/LLM agents.

---
# PART C. config-driven-transforms (pt1)
URL: https://bradmurry.com/software/config-driven-transforms/ (file `32-config-driven-transforms-pt1.md`; page heading "Config Driven Data Transforms (pt1)")

## C1. Motivation: move change from code deployment to configuration
- Layer: BP
- Source: https://bradmurry.com/software/config-driven-transforms/ , 32-config
- Evidence: "Areas of a system where data is exchanged are typically hotspots for change and is why we want to move that change from a code deployment requirement to a configuration concern."
- Also: "There's no money in manually manipulating data." and "too much bespoke code is created for the sole purpose of transforming one shape of data to another."
- Confidence: High.

## C2. Five DSL selection criteria
- Layer: MOD
- Source: same
- Evidence: "several aspects should be considered when choosing or developing a DSL for your transformation needs." followed by list: Learning Curve, Performance, Validation, Versioning, Testing.
- Use-case range: "reducing the maintenance costs within the codebase, to providing self-serve data transformations for non technical users, or somewhere in between"
- Confidence: High. Note: "Performance" is listed but has no dedicated section in pt1.

## C3. Source/sink path-mapping DSL ("schleppy transforms")
- Layer: IMPL
- Source: same, "Example using schleppy transforms"
- Evidence: "Simple reshaping like this can be achieved using sink and source alone in the transform def below:"
- Code shape (verbatim fragments): `'source': {'path': 'data'}`, `'sink': {'path': {` with entries such as `'data.name': 'source.name'`, `'data.contact.phone': 'source.phone'`, `'data.location.gps.lat': 'source.latitude'`; executed via `TransformProcessor(execution_context, transformer)` then `result = processor()`.
- Behavior: flat source JSON (name/phone/email/address/latitude/longitude) reshaped into nested `data.contact` / `data.location.gps`.
- Confidence: High. Note: post calls the example "schleppy transforms" but imports `plot.transform.processor`; naming not explained.

## C4. Learning curve
- Layer: MOD
- Source: same
- Evidence: "Simple path traversal to reshape data has a low learning curve, as the DSL is represented by the data itself."
- Limit: "Often, we'll need to pull in more powerful tools depending on our transform needs, but a little bit can go a long way."
- Confidence: High.

## C5. Validation at four stages
- Layer: MOD
- Source: same, "Validation"
- Evidence: "Validation via standard registries is key to ensuring data integrity, and schema checks can be used during several stages of the transform cycle:"
- Stages (verbatim): "Pre-transform (make sure the incoming data is in the right shape)"; "Pre-step (when there is a multi-step DAG for the transformation, we can make sure the section being transformed is in the right shape)"; "Post-step (check that the transform produced the desired shape)"; "Post-transform (final guarantee that the transform was successful)"
- Priority: "The most important schema validation is usually the Post-transform, but having several checks (especially with complex transformations) can help pin down potential quality issues."
- Confidence: High.

## C6. Versioning
- Layer: MOD
- Source: same
- Evidence: "By moving transformation into a DSL document, the same code can process multiple versions of a transformation, making version tracking and evolution much easier."
- Confidence: High.

## C7. Testing: format-agnostic JSON in/out
- Layer: IMPL
- Source: same
- Evidence: "An ideal test framework is the one consuming teams use on their existing services. This means we should make our transformation process as agnostic as possible."
- Format: "JSON input files -> Transformation(s) -> Output JSON files"
- Benefit: "Providing test and result data in a standard format makes it easy for teams to test and iterate on their transforms, even before deployment."
- Also: "This also enables the team creating the transform to use the test framework of their choosing for making assertions."
- Confidence: High.

Cross-links: pt1 only; later parts not in this file. No mention of AI/LLM. Related thematically to dsl-ai-partition (Part F) via DSL/config-over-code.

---
# PART D. sparkctl
URL: https://bradmurry.com/software/sparkctl/ (file `42-bm-sparkctl.md`; "sparkctl: Config Driven Model Serving for DGX Spark"; dated Jul 13, 2026, last updated Jul 15, 2026)

## D1. Problem and origin
- Layer: BP
- Source: https://bradmurry.com/software/sparkctl/ , 42-sparkctl
- Evidence: "For several internal projects, we leverage open weight models and fine tune them for the task(s) at hand."
- Evidence: "A lot of that post training happens in the cloud, but for POC and experimentation, we run locally when we can. On the evaluation side of things, this has proven to be a massive cost optimization, and we’re seeing ROI on hardware already."
- Pain: "Every model I wanted to test meant the same rituals: ssh into a node, kick off a HuggingFace download, sometimes watch it stall at 80% because the downloader hung"
- Confidence: High (author-reported, no figures).

## D2. Core principle: configuration is the source of truth (kubectl-style, desired state)
- Layer: MOD
- Source: same, "How it started"
- Evidence: "The idea is simple: configuration is the source of truth. Topology lives in `cluster.yaml`, model configurations live in `recipes/*.yaml`, and the CLI makes reality match the config."
- Grammar: "the grammar is lifted straight from kubectl."
- Closing statement: "I wanted `apply` to mean the same thing for models that it means for everything else in my life: describe the end state, let the tool close the gap."
- Tool description: "a small Python CLI and orchestration layer for one or more DGX Sparks."
- Confidence: High.

## D3. Two file kinds: cluster.yaml and recipes
- Layer: IMPL / MOD
- Source: same, "The shape of the thing"
- Evidence: "Everything is driven by two kinds of files. `cluster.yaml` describes your hardware and how the control plane should run"
- cluster.yaml keys shown: `cluster` (name, head, container_image, model_cache, api_port), `deploy` (user, remote_path), `backend: docker`, `server` (mode: local, host, port: 8080), `nodes`.
- Recipe: "And a recipe describes a deployable set of model services". Example `qwen3-coder-30b` with services `agent` (engine: vllm, model, served_name, node, `parallel: { tensor: 1 }`, port, max_model_len, gpu_memory_utilization, tool_call_parser) and `embeddings` (engine: ollama, model nomic-embed-text).
- Mixed engines: "A recipe can mix engines. Here a vllm agent and an Ollama embeddings model deploy together as one unit, which turns out to be exactly what most of my agentic workloads want."
- Confidence: High.

## D4. Command model (apply/get/logs/status/top/serve/pull)
- Layer: IMPL
- Source: same
- Execution context: "The CLI runs from your laptop and figures out context by hostname. Read commands run locally and reach out to the nodes over ssh. Mutating commands push the repo to the nodes first and then run on the head."
- `apply`: "`apply` does the whole dance: makes sure the weights exist (downloading and verifying if they don’t, more on that below), tears down whatever was running, repoints the `current` marker, and brings the new recipe up."
- Resilience: "The boot daemon serves whatever recipe is current, so a power blip doesn’t cost you a morning."
- Inspection: `sparkctl get recipes`, `sparkctl get services`, `sparkctl logs agent -f`, `sparkctl top nodes`, `sparkctl get models`.
- Setup commands: `deploy --init`, `build`, `secret set HF_TOKEN`.
- Confidence: High.

## D5. Drift detection via recipe sha256 manifest
- Layer: MOD / IMPL
- Source: same
- Evidence: "every deploy writes a manifest with the recipe’s sha256 to each node, and `sparkctl status` compares what is actually running against what the config says should be running. It tells you “matches current” or “DRIFT”, not just “a container exists”."
- Confidence: High.

## D6. Recipe-generation wizard
- Layer: IMPL
- Source: same, "You don’t have to write the yaml by hand"
- Evidence: "The wizard recognizes the model family and fills in the parts that used to send me back to the docs, the right parsers, sensible sizing, and defaults tuned for a Spark"
- Outcome: "Either way you end up with a committed, versioned recipe instead of a one-off command, which was the whole point."
- Command: `sparkctl create recipe`; example detects "gpt-oss - harmony format is built in - no tool/reasoning parser needed".
- Confidence: High. Note: wizard is rule/prompt-based; the post does not say it uses an LLM.

## D7. Unified OpenAI-compatible endpoint (relation to LLM/agent serving)
- Layer: IMPL
- Source: same, "One endpoint for everything"
- Evidence: "Your tools want a stable OpenAI compatible endpoint that doesn’t change every time you move a model between nodes. That’s the unified server"
- Mechanism: "It runs LiteLLM under the hood (no reason to reinvent model routing), generates the route table from whatever is currently deployed, and restarts itself whenever you apply a new recipe."
- Consumers: "Point your agents, IDE, whatever at `localhost:8080/v1` and stop thinking about which node serves which model."
- Load balancing: "If two nodes serve the same `served_name`, they automatically become a load balanced pool."
- Endpoints: "/v1 /metrics /dash /healthz".
- Confidence: High.

## D8. Relation to AI/LLM serving and agentic workloads (explicit statements only)
- Layer: BP / IMPL
- Source: same
- Evidence: "Multiply that by testing, benchmarking, and agentic serving workloads, and I was spending more time shuffling files than actually using the models."
- Evidence: "which turns out to be exactly what most of my agentic workloads want." (agent LLM + embeddings model as one deployable unit)
- Recipe fields with agent relevance: `tool_call_parser: qwen3_coder`; wizard handles "tool-call parser" and "reasoning parser".
- Gap: the post does not describe any agent pattern (FSM, reducer, HITL); it is model-serving infrastructure. Relation to agents is only as the hosting/endpoint layer for agents.
- Confidence: High (that these statements exist); no deeper agent architecture to extract.

## D9. Metrics aggregation
- Layer: IMPL
- Source: same
- Evidence: "Every vllm instance already exposes Prometheus metrics, so the server scrapes each node and re-exposes the whole cluster on a single `/metrics` endpoint"
- Node-level: "along with node level basics that work even for engines that expose nothing (unified memory, GPU utilization, disk)."
- Optional: "set `server.grafana.enabled: true` and sparkctl provisions Prometheus and Grafana containers pointed at that single metrics endpoint, dashboard included."
- Confidence: High.

## D10. Multi-node tensor parallelism vs replicas, as a one-line recipe change
- Layer: IMPL
- Source: same, "Splitting a model across nodes"
- Evidence: "`tensor: 2` with no node pin tells sparkctl this service spans the cluster. On `apply` it starts Ray containers on the head and worker, wires up the NCCL environment for the fabric"
- Alternative: "run the same 30B model independently on both nodes with the same `served_name`, and the gateway load balances two concurrent agents across them."
- Summary: "Big split model or two fast replicas is a one line difference in a recipe"
- Client transparency: "Your client code cannot tell the difference."
- Confidence: High.

## D11. Weight integrity: verified pull, stall watchdog, sha256 per shard
- Layer: IMPL
- Source: same
- Evidence: "`pull` downloads once on the head with a stall watchdog (the modern HuggingFace downloader has a known hanging problem, so sparkctl kills and resumes stalled downloads automatically), then verifies every shard by comparing its sha256 against the blob name, then mirrors to the other node over the fabric and verifies again."
- Invariant: "A corrupt shard cannot reach a serve. That property is not negotiable in this tool."
- Queue: `pull-queue` "runs it detached on the head, so it survives my laptop going to sleep".
- Confidence: High.

## D12. Pluggable NAS weight distribution (resolution order)
- Layer: IMPL
- Source: same, "The NAS option"
- Evidence: "`apply` resolves weights in order: already on the nodes, done. On the NAS, replicate from there over the local network instead of the internet. Nowhere, download once straight to the NAS, replicate out, and next time it’s a replicate. Every hop still gets the sha256 verification treatment."
- Config: `nas: mode: path` (NFS mount) or `mode: ssh` (rsync endpoint).
- Confidence: High.

## D13. Pluggable execution backend seam (docker default, k8s stub)
- Layer: MOD / IMPL
- Source: same, "Beyond docker"
- Evidence: "the execution layer is deliberately a plugin. There’s a `backend:` key in `cluster.yaml`, and the entire rest of the tool (the CLI verbs, the gateway route table, the metrics aggregation) talks to a small interface rather than to docker directly."
- Interface: "The gateway only ever asks a backend “where do models serve” and the metrics side only asks “what do I scrape”, so a backend that answers those questions gets the whole control plane for free."
- Status: "The k8s backend is a stub today, it fails loudly and politely if you select it."
- Design stance: "Making the runtime a config key means the homelab default stays simple and the escape hatch is already there."
- Confidence: High.

Cross-links: references github.com/bradodarb/sparkctl (outside the 18 URLs; not followed). Conceptual echo (not an explicit link) of the devops-contract idea (Part B) of a stable interface above swappable runtimes; stated by this gatherer only as an observation for verification phase, not asserted by the author.

---
# PART E. ai-annotations
URL: https://bradmurry.com/software/ai-annotations/ (file `44-bm-ai-annotations.md`; "AI Annotations: Embedding Context in Your Codebase"; Jan 11, 2026)

## E1. Motivation: persistent context embedded in the codebase
- Layer: BP
- Source: https://bradmurry.com/software/ai-annotations/ , 44-annotations
- Evidence: "AI annotations take them further by embedding metadata directly in your codebase. When AI agents read annotated files, they automatically receive enriched context that informs multiple prompts and interaction patterns without manual intervention."
- Evidence: "instead of repeatedly explaining the same constraints or intentions across sessions, you annotate once and every agent benefits."
- Author background: "experimenting with approaches ranging from pure “vibe coding” to rigorous spec-driven development, I’ve learned that prompt engineering and context management remain critical to successful AI collaboration."
- Confidence: High.

## E2. Why in-code (vs external wiki): sync, git audit trail
- Layer: BP
- Source: same
- Evidence: "Git naturally provides an audit trail of how annotations evolve alongside code, creating a living record of human-AI collaboration."
- Evidence: "inline annotations stay synchronized with the code they describe: they’re versioned, reviewed in pull requests, and refactored when the code changes."
- Evidence: "The information remains accurate and relevant because it lives where developers actually work, not in a separate system that inevitably drifts out of sync."
- Confidence: High.

## E3. The Agent Annotation Standard: `@!` prefix
- Layer: MOD
- Source: same, "What Are AI Annotations?"
- Evidence: "The Agent Annotation Standard provides a unified syntax for embedding semantic metadata in code and documentation. Using the `@!` prefix, it enables developers, reviewers, and AI agents to attach machine-readable directives to any textual artifact."
- Example lines: `# @!readonly true (author: alice)`, `# @!canon "error_handling" (pattern: result_type)`, `# @!deprecated "Use process_data_v2 instead"`, `# @!todo "Handle edge cases" (tags: [bug critical])`.
- Confidence: High.

## E4. Two syntax styles: inline and block
- Layer: IMPL
- Source: same
- Inline: "Place a single-line annotation before any code element: functions, methods, classes, or at the top of files:" form `@!<annotation-key> [<annotation-value>] (<properties>)`
- Block: "Define precise boundaries for annotated regions spanning multiple lines:" form `@!begin <key> [<value>] (<props>)` ... `@!end <key>`
- Confidence: High.

## E5. Toon Format for properties
- Layer: IMPL
- Source: same, "Why Toon Format?"
- Evidence: "Properties in annotations use Toon Format, a lightweight data notation designed for readability and simplicity."
- Advantages listed (vs JSON/YAML): "No quotes required for simple keys and values"; "More compact - fewer characters for the same data"; "Human-friendly - optimized for inline use rather than configuration files"; "Simpler parsing - especially in single-line contexts".
- Example: `# @!metadata (author: alice priority: high tags: [backend critical])`.
- Confidence: High.

## E6. Core annotation keys
- Layer: IMPL
- Source: same, "Core Annotation Keys" (table flattened in extraction)
- Evidence (rows): "`readonly`Marks code as immutable"; "`agent`Target specific automation"; "`canon`Marks canonical reference implementation"; "`metadata`Custom key/value pairs"; also `link` (external resources), `todo` (task description), `license` (declares licensing). `deprecated` appears in examples but not in the key table.
- Confidence: High.

## E7. Canonical references (`@!canon`) to guide AI toward patterns
- Layer: MOD
- Source: same, "Canonical References"
- Evidence: "Establish authoritative patterns, architectures, and standards that AI agents and developers should follow:"
- Examples: repository pattern (`@!canon "repository_pattern" (type: architecture layer: data_access)`), error handling (`@!canon "error_handling" (pattern: result_type tags: [backend])` with comment "All async operations should return Result<T, Error> instead of throwing"), naming convention (`@!canon "naming_convention" (scope: api_controllers)`).
- Confidence: High.

## E8. How AI uses annotations (AI Collaboration use cases, verbatim)
- Layer: MOD / IMPL
- Source: same, "AI Collaboration"
- "Context enrichment: Annotations automatically inform LLM context without manual prompt engineering"
- "Dynamic routing: Use tags to filter which code snippets feed into specific AI workflows"
- "Constraint communication: Mark sections as readonly, experimental, or deprecated for AI agents"
- "Pattern enforcement: Use `@!canon` to guide AI toward established architectural patterns"
- Confidence: High. Gap: the post gives no description of how an agent runtime actually ingests/parses annotations (no agent-side implementation beyond the sample regex parser); it says "no special tooling to get started".

## E9. Tooling integration (non-AI consumers)
- Layer: IMPL
- Source: same, "Tooling Integration"
- "Linters: Skip or enforce rules based on annotations (`@!agent "linter"`)"
- "Code review bots: Auto-comment on TODOs, link to documentation"
- "CI/CD: Enforce policies, gate deployments based on annotation metadata"
- "Pattern validation: Tools can verify new code follows canonical examples"
- "Read-only sections: `@!readonly true` – CI can flag unauthorized modifications"
- Confidence: High.

## E10. Claimed benefits table
- Layer: BP
- Source: same, "Real-World Benefits" (table flattened)
- Rows (verbatim): "Persistent contextAnnotate once; every AI session benefits"; "Multi-agent collaborationSingle source of truth for human and AI developers"; "Audit trailSee how constraints and intentions evolve over time"; also Version controlled, Synchronized documentation.
- Confidence: Medium - benefits are asserted without measurement; heading says "Real-World" but no case is shown.

## E11. Language support, sample parser, roadmap
- Layer: IMPL
- Source: same
- Language: "Annotations work within any language’s comment syntax" (examples `//`, `#`, `<!-- -->`).
- Parser: minimal Python regex parser (`ANNOTATION_RE`, `parse_annotations(file_path)`), matches `# @!key value (props)` only for `#` comments.
- Roadmap: "IDE integration: Hover tooltips showing annotation metadata"; "AI-driven generation: Auto-suggest annotations based on code context"; "Standardized schema: Central registry for annotation validation"; "Cross-language libraries: Parser implementations for Rust, Go, JavaScript"
- Status: "The Agent Annotation Standard is evolving."
- Confidence: High.

Inconsistency noted: Getting Started suggests `tags=["frontend", "critical"]` while examples use `(tags: [bug critical])` Toon form (different syntax within the same post).

Cross-links: external spec pages (toon-format/spec, "Full specification") outside the 18 URLs; not followed.

---
# PART F. dsl-ai-partition
URL: https://bradmurry.com/software/dsl-ai-partition/ (file `43-bm-dsl-ai-partition.md`; "Safe(r) Deterministic AI Code Generation Through DSL Partitioning"; Jan 17, 2026)

## F1. Problem: AI code generation velocity vs risk, non-determinism
- Layer: BP
- Source: https://bradmurry.com/software/dsl-ai-partition/ , 43-dsl
- Evidence: "AI can write code faster than you can review it."
- Evidence: "AI-generated code can be insecure, untested, unpredictable. Worse, it’s non-deterministic. Ask an LLM to implement the same requirements twice and you’ll get completely different code."
- Evidence: "we want the velocity, but can’t afford the risk."
- Confidence: High.

## F2. Core idea: DSL as capability boundary between AI orchestration and human critical components
- Layer: MOD
- Source: same, "A Solution: Partition Generation with a DSL"
- Evidence: "Don’t fight AI code generation. Constrain where it operates."
- Evidence: "A Domain-Specific Language sits between AI-generated orchestration and human-written critical components. The DSL acts as a capability boundary: AI can only express operations the DSL supports, and the DSL only exposes safe, pre-vetted primitives."
- Claim: "This narrows the interaction surface, making AI output deterministic and predictable. Same requirements, same DSL output."
- Confidence: High (explicit); the determinism claim is asserted via a constructed example, not tested empirically (see F5).

## F3. Partition table: who writes what (three layers)
- Layer: MOD
- Source: same, "Who Writes What?" (table flattened in extraction)
- Evidence rows: "Critical CoreHumans + careful AIMonthlyExhaustiveAuth, DB access, encryption"; "DSL RuntimeHumansMonthlyThoroughParser, executor, validators"; "Business LogicAI + oversightDaily/hourlySchema + access validationWorkflows, transforms, rules"
- Parsed reading (from the flattened columns Who Writes / Change Frequency / Testing / Examples): Critical Core = humans + careful AI, monthly, exhaustive testing; DSL Runtime = humans, monthly, thorough; Business Logic = AI + oversight, daily/hourly, schema + access validation.
- Note: "AI can help with core components, but those PRs require stringent review"
- Confidence: High for the row text; column boundaries inferred from the table header text "LayerWho WritesChange FrequencyTestingExamples" (flattened).

## F4. Runtime guarantees (whatever the AI generates)
- Layer: MOD / IMPL
- Source: same, "Speed With Reduced Risk"
- Evidence: "But the DSL runtime ensures that no matter what the AI generates:" then list: "Schema validated before execution"; "Access permissions checked and unauthorized operations gated"; "Only approved APIs are called"; "Data access goes through validated queries"; "Execution stays within resource limits"
- Speed claim: "Changes happen in hours, not days."
- Confidence: High.

## F5. Determinism through constraint (canonical representation)
- Layer: MOD
- Source: same, "Determinism Through Constraint"
- Evidence: "Both work. But they’re syntactically different, use different variable names, different structures. Code reviews are tedious. Testing is inconsistent. Git diffs are noisy." (about two generated `process_order` functions)
- DSL contrast: "Limited vocabulary. Enforced structure. Single canonical representation." (example `discount_rule: threshold: 100 rate: 0.10` identical across "Generation 1" and "Generation 2")
- Benefits list: "Predictable code review: Clear, scannable diffs"; "Consistent testing: Same test works every time"; "Unified debugging: Same error format always"; "Meaningful version control: Each commit is a semantic change"
- Automation claim: "When AI output is predictable, you can build automation that relies on it: bulk testing, static analysis, automated migrations, documentation generation."
- Confidence: High that it is stated; Medium on the substance - the two "generations" are illustrative, not an experiment, and identical output for a trivial two-field rule does not show identical output for complex requirements.

## F6. Governance built in
- Layer: MOD
- Source: same, "Built-In Governance"
- Evidence: "The DSL is your governance layer:" then "Version control: DSL files tracked in git"; "Code review: Even AI-generated changes get PR’d"; "Schema validation: DSL structure validated before execution"; "Access control: Permissions checked, unauthorized operations gated"; "Audit trail: Every execution logged with full context"; "Rollback: Revert the DSL file"; "Testing: DSL expressions can be unit tested"
- Confidence: High.

## F7. Progressive complexity (three steps)
- Layer: IMPL
- Source: same, "Progressive Complexity"
- Evidence: "Start simple, expand as needed:" Step 1 basic transforms (`transform:` with `map`/`filter`), Step 2 multi-step workflows (`workflow:` with `validate`, `call`, `send`, `log`), Step 3 complex rules (`decision_tree:` loan_approval with `if/then/default`).
- Confidence: High.

## F8. Contract between AI and human tracks: AI-assistance rules by layer
- Layer: MOD
- Source: same, "AI Can Help, But With Guardrails" (table flattened)
- Evidence: "AI roleGenerates complete implementationAssists human developer" (DSL layer generates complete implementation; core layer assists a human developer)
- Review: "Review1-2 reviewers, standard process2-3+ senior engineers, stringent" (DSL layer 1-2 reviewers, standard; core 2-3+ senior engineers, stringent)
- Deployment/approval: "DeploymentHoursDays/weeks with staged rollout"; "ApprovalTeam leadArchitect + security sign-off"
- Testing: DSL layer "Schema + access validation + integration"; core "Unit + integration + security + perf"
- Confidence: High for row text; column assignment inferred from flattened header "AspectDSL LayerCore Layer".

## F9. Two-track model and routing rule between tracks
- Layer: MOD
- Source: same, "The Two-Track Model" and "When to Update Core vs DSL"
- Fast Track: "AI generates complete workflows"; "Deploy frequently (multiple times daily)"; "Low risk (sandboxed)"; "Team lead approval"
- Careful Track: "AI assists humans"; "Thorough review (days to weeks)"; "High risk (full system access)"; "Architecture team approval"
- Rationale: "This maximizes velocity where risk is lower (DSL layer) while enforcing rigor where it’s critical (core layer)."
- Extend DSL when: "Requirements fit existing primitives"; "New combination of existing operations"; "Customer-specific logic"; "Experimental feature"
- Update Core when: "Completely new capability (new API, data source)"; "Performance optimization at runtime"; "Security requirements changed"; "Bug fix in existing component"
- Proportion claim: "The DSL constrains 90% of changes to fast track. Only 10% need the careful track." (no supporting data in the post)
- Confidence: High for stated rules; the 90/10 split is an unsupported assertion.

## F10. Worked example: adding SMS support (gap discovery loop)
- Layer: IMPL / MOD
- Source: same, "Example: Adding SMS Support"
- Flow: AI-generated/desired DSL uses unsupported `type: sms` ("Not supported yet!") -> engineering ticket with requirements (Twilio, rate limit, PII, delivery tracking, error handling; "AI Assistance: Medium", "Review: Stringent") -> human writes interface (`NotificationService.send_sms(recipient, message, idempotency_key)`), AI implements, "Developer reviews line-by-line" -> review checklist -> extend `dsl/operations.yaml` with `send_sms: maps_to: NotificationService.send_sms`, parameter types, `rate_limit: 100_per_minute`, `security_level: pii_handling` -> "Now AI can use it".
- Checklist items (verbatim samples): "Security: No SQL injection, XSS, auth bypass, credential leaks"; "Test coverage: >90%, security tests included"; "Deployment plan: Staged rollout, monitoring, rollback"
- Confidence: High.

## F11. Worked example: payment workflow (operation-to-core mapping with parameter constraints)
- Layer: IMPL
- Source: same, "Real-World Example: Payment Workflow"
- Evidence: "Critical Core (human-written, audited):" `PaymentService` with `charge_card`/`refund`; DSL operations map `payment.charge` -> `PaymentService.charge_card` with constraints `amount: {type: decimal, min: 0.01, max: 10000}`.
- AI-generated workflow: comment "Generated by AI: \"Create subscription renewal with retry logic\"" using `operation:` references, `retry: max_attempts: 3 backoff: exponential`, `on_error: goto: handle_failure`.
- Note: heading says "Real-World Example" but code is illustrative; no deployment evidence in the post.
- Confidence: High (as description of mechanism).

## F12. Implementation patterns: YAML/JSON first, embedded DSL (Lua) for complexity, schema validation, DSL testing
- Layer: IMPL
- Source: same, "Implementation Patterns"
- YAML/JSON: "Pros: Easy to parse, validate, version control"; "Cons: Limited expressiveness for complex logic"; "Best for: Workflows, data transforms, simple rules"
- Embedded: "For sophisticated logic, use embedded DSLs in safe languages:" (Lua example) "Cons: Requires sandboxing, more attack surface"; "Best for: Complex calculations, business rules, pricing"
- Schema: JSON Schema draft-07 for workflow (`name` pattern `^[a-z_]+$`, required `name`, `steps`).
- Testing: "Make DSL testing trivial:" via `DSLRuntime(services=mock_services)`.
- Confidence: High.

## F13. Security: sandbox, audit, review pipeline
- Layer: IMPL
- Source: same, "Security Considerations"
- Sandbox: "Resource limits: CPU, memory, execution time"; "Network isolation: Control external service calls"; "Data access: Enforce row-level security, field masking"; "Capabilities: Explicitly grant permissions per DSL file"
- Audit: log record with `dsl_file`, `dsl_version`, `triggered_by`, `operations_executed`, `duration_ms`, `success`.
- Review process (verbatim steps): "AI generates DSL"; "Automated schema validation (structure, syntax)"; "Automated access validation (permissions, authorized operations)"; "Automated tests run against mocks"; "Human review approves"; "Staged rollout: dev → staging → prod"
- Confidence: High.

## F14. Getting started and bottom line
- Layer: BP
- Source: same
- Steps: "Pick one workflow: Start with a single, non-critical process"; "Extract the core: Pull critical components into services"; "Design minimal DSL: 3-5 operations covering 80% of use case"; "Have AI generate it: Use your preferred model"; "Test rigorously: Validate, test, review"; "Iterate: Learn and expand"
- Summary: "AI code generation is too valuable to avoid, too risky to use directly."; "AI moves fast on business logic"; "Humans maintain security and infrastructure"; "The DSL enforces boundaries and governance"
- Provenance claim: "The architecture emerged from real experience integrating AI into enterprise systems."
- Confidence: High. The "80%" and "3-5 operations" numbers are unsupported assertions.

Cross-links: none to the 18 URLs inside the post. Conceptually adjacent (author-implicit, not linked) to config-driven transforms (Part C: DSL document, schema validation, versioning, testing) and to the deterministic-shell posts (other gatherer).

---
# PART G. pyflink-intro
URL: https://bradmurry.com/software/pyflink-intro/ (file `45-bm-pyflink-intro.md`; "PyFlink Series (Intro)"; Jul 15, 2023). Short intro (approx. 75 lines); no agent content.

## G1. Why stream processing: needs list
- Layer: BP
- Source: https://bradmurry.com/software/pyflink-intro/ , 45-pyflink
- Evidence: "Orchestrate ML/AI training and augmentation pipelines" (one of listed needs alongside "Provide near real time (NRT) capabilities", "Support Event Driven Architecture", "Stream your data and process in flight", "Reduce size and costs of traditional data lake patterns", "Modernize your data engineering efforts")
- Confidence: High.

## G2. Micro-batching is not streaming
- Layer: BP / MOD
- Source: same
- Evidence: "You can batch with streaming, after all that’s just a bounded stream of batch size (n)."
- Evidence: "no matter how “micro” you make your batches in a batch-oriented system, you’ll never quite achieve streaming."
- Qualifier: "This is especially true when you consider higher ordered events."
- Motivation: "organizations are trying to reduce latency in their data delivery so that the data is more actionable."
- Confidence: High.

## G3. Why PyFlink
- Layer: IMPL
- Source: same, "Why (Py)Flink?"
- Evidence: "the Apache Flink ecosystem has us covered with PyFlink, pythonic bindings to the underlying JVM constructs using Py4J."
- Evidence: "Using the Py4J bridge, you can directly interact with “real” Flink JVM objects from python, and Flink’s various APIs can call back to your python objects as well."
- Rationale: "Perhaps python’s vast ecosystem of packages offer the best fit for your use case (ML anyone?)."; "You may have use cases that can’t be easily resolved with Flink’s pure SQL interface, but organizational issues prevent the use of JVM languages directly."
- Caveat: "**Some (very)high throughput use cases may have performance impacts when using PyFlink vs JVM directly, but Flink has escape hatches."
- Confidence: High.

## G4. Setup scope of the intro
- Layer: IMPL
- Source: same, "Let’s dive in!"
- Evidence: "We’re not going to get into any specific processing goals in this post, instead we’ll focus on getting a local setup running that can then be deployed to AWS MAF."
- Setup: `make fresh` (virtualenv + deps); "Make sure you have python 3.8 or greater installed as well as the `virtualenv` package." Next: "Up next: Part 1 (Basic Data Stream API Job)".
- Confidence: High. Thin post; no pattern content beyond the above. Uses a `make` target as the setup command (same Makefile-as-interface idea as Part B, observation only).

---
## Declarative Conclusions
Not applicable (external sources only; no local human sources such as transcripts).

## Rejected Information

| # | Information | Source | Rejection Reason | Re-include If |
|---|---|---|---|---|
| 1 | CNN/NBC biography, COVID and election-politics narrative | 30-apis "A little background" | Narrative context, no pattern content (only the 2-month deadline and threat context used) | Credibility/provenance of the outcome claims is challenged |
| 2 | Per-language Makefile command bodies (npm, jest, tsc, twine, sls) | 31-devops | Illustrative tool commands, not contract mechanics (summarized in B8) | Implementation detail of the contract on a specific stack is needed |
| 3 | Full sample source/target JSON of the transform example (balance, friends, tags, "about" text, etc.) | 32-config | Example data, no mechanism beyond the sink/source mapping | Transform semantics (e.g. unmapped fields) become relevant |
| 4 | DGX Spark hardware specifics (200G fabric, CX7, NCCL dual rail, unified memory/nvidia-smi N/A) | 42-sparkctl | Hardware-specific; outside agent-pattern scope | Local GPU model-serving topology is studied |
| 5 | Terminal output tables/dashboards (`get services`, `top nodes`, `get models`) | 42-sparkctl | Display detail | CLI UX is in scope |
| 6 | SMS/PaymentService code bodies and 9-point review checklist details beyond summary | 43-dsl | Illustrative code; mechanism captured in F10/F11 | Review-gate content needed verbatim |
| 7 | Toon Format spec, external "Full specification" pages | 44-annotations | Linked outside the 18-URL list; not followed per plan | Annotation grammar details are needed (not in this post) |
| 8 | "Part 1 (Basic Data Stream API Job)" and the GitHub repo | 45-pyflink | Not among the 18 URLs; not followed | Later PyFlink posts are added to scope |
| 9 | Images/illustrations (alt text "Reactive API" on three differing diagrams) | 30-apis | Alt-text mislabel of the pasted images; no content | Diagrams are provided |

## Actor Relevance
Not applicable: the plan lists no actors ("None — research is actor-agnostic").
