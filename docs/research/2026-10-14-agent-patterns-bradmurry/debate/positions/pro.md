# Debater: pro, Round 1, position

Scale: STRONG = primary/independent source (several groups); MEDIUM = one explicit source; WEAK = one author's self-description.
Citation: [file#section]. Abbreviations: 00 = analysis/00-summary.md, 01 = analysis/01-cross-source-verification.md, LSR = 2026-10-14-llm-self-research/README.md.

## Thesis
It is worth implementing a **deterministic shell around a probabilistic model** as an overriding principle. This principle is consistent with the independent literature. There is no need to implement Brad's "Aegis" runtime: its ideas can be implemented in .NET with plain code, and the runtime itself has not been independently verified.

## Claims

1. **The model brings value, and control sits outside the model (reducer: state = typed context, structural legality, not "a request to transition").** It prevents "LLM as a state machine", i.e. step drift and jumps over validation (e.g. order confirmation without verification). The direction is independently confirmed by Zheng 2023 and Huang 2023 (the LLM does not correct itself without an external signal) and by Anthropic ("predefined code paths") [01#2; LSR#1,#3]. **STRONG as to direction.** The reducer runtime itself is **MEDIUM/WEAK**: one author, "the glue code is gone" was judged Unsupported [01#4 D1].

2. **Loop limits outside the model (maxIterations, cap of 128 microsteps, budget `size(drafts)<4`, state `stalled`).** It prevents infinite loops and uncontrolled cost. Anthropic: "Stopping conditions, such as a maximum iteration count" [LSR#8]. **STRONG.** Caveat taken from the author's correction: the no-progress guard does not span turns, so a cross-turn limit has to be added yourself [01#1 C1].

3. **Patterns 3 (Supervisor/Router, enum `route`) and 8 (Planner/Executor) as an intent classifier.** The model output is an enum with a finite number of values (FAQ / order status / return / human), and the further flow is in code. It prevents free-text "decisions" by the model. Consistent with Anthropic's *routing* pattern [LSR#1,#5]. **STRONG as to the pattern, MEDIUM as to Brad's implementation.**

4. **Pattern 9 (Generator/Critic/Refiner) only with an external signal.** The critic should be a deterministic check: whether the SKU exists in the catalog, whether the URL returns 200, whether the price matches the database. "LLM-as-judge" serves solely as an auxiliary signal. This prevents hallucinated products and prices (LLM09 Misinformation). Zheng, Huang and Brad agree that the judge is weak (position/verbosity/self-enhancement bias) [01#2]. **STRONG.** Reflexion (91% HumanEval) is the authors' claim [LSR#3].

5. **Idempotency, journal, replay, "timeout is UNKNOWN, not FAILED" (Agents in Production).** The repo already has Outbox/Inbox and sagas, so this is a simple continuation of existing discipline. It applies to actions with an effect (refund, cancellation): an idempotency key, a run record, no automatic retry on an unknown outcome. It prevents double refunds and duplicated orders. The pattern itself is classic in distributed systems. Its transfer to an agent is described only by Brad: **WEAK/MEDIUM** [00#A].

6. **Cheapness without changing quality: prompt caching + routing to an SLM.** Anthropic documentation: read 0.1x, write 1.25x (TTL 5 min) or 2x (1 h), an identical prefix is required, changing the tools invalidates the cache. The store's prefix (system prompt, FAQ, policies) is ideal for caching [LSR#6]. **STRONG** (official documentation). SLM: an NVIDIA position paper, the authors themselves call it a "value statement" [01#4 D6], so **MEDIUM**. I omit the "10-30x cheaper" figures from blogs.

7. **Chatbot security rules: key only on the server, no change permissions, cost budget, OWASP LLM01/06/10.** It prevents prompt injection and excessive agency. Actions only through existing, authorized endpoints [LSR#9]. **STRONG** (OWASP) for the risk list. Details from blogs are **WEAK**.

8. **The self-evolving loop (RUN/MEASURE/DISCOVER/BUILD) and prompt optimization (GEPA/DSPy).** The rules "one change at a time, report the regression" are cheap and healthy. Brad's metrics (e.g. 612 400 -> 26 400 tokens) are a self-description without trials [00#Gaps]. GEPA is the authors' claim, not independently verified [LSR#4]. **WEAK/MEDIUM.** Therefore: only as a late step, after building an evaluation set.

## What I do not defend
Multi-agent debate and quorum: the sources contain no data at all on effectiveness [01#2, rating Low]. AutoGen group chat itself declares "not meant to be used in real applications" [01#1 C10]. Long-term memory (Mem0, MemGPT): the claims of the product's authors and GDPR risk [LSR#7]. One part of "Aegis" (Event-Driven) is "half real" [00#B].

## Implementation order
1. **Security and cost skeleton (week 1):** backend proxy, `max_tokens` and a per-user budget, no write tools, prefix prompt caching, measuring `cache_read_input_tokens`. First slice: an FAQ/policy chatbot without any actions.
2. **Deterministic shell (weeks 2-3):** intent router as an enum (pattern 3), iteration limit, deterministic SKU/URL/price check before sending the answer (pattern 9 with an external signal), run journal. Read-only actions only (order status through an authorized endpoint).
3. **Evaluation and optimization (later):** a test question set, an LLM judge calibrated on human labels, only then an SLM for routing and possibly GEPA. Actions with an effect (refunds) only with idempotency and a journal (claim 5).
