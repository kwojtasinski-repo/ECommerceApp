# Self research: LLM/agent concepts + a chatbot on the website + learning models

- **Research date:** 2026-10-14 (sources fetched/searched on that day; publication dates next to each source)
- **Mode:** the assistant's own research (WebSearch + WebFetch), **not** via the `research-gather` workflow — no independent cross-verification by subagents.
- **Related:** [`../2026-10-14-agent-patterns-bradmurry/`](../2026-10-14-agent-patterns-bradmurry/) (main research from Brad Murry's blogs + AutoGen)
- **Confidence rating rule:** **High** = primary source (paper/official documentation) fetched and quoted; **Medium** = single source or a paper known only from its abstract; **Low** = blogs/secondary summaries, unverified numbers.
- **What I did NOT do:** I did not read whole papers (only abstracts/official pages), I did not run any code, Medium was skipped at your request.

> **Interpretive assumptions (correct me if wrong):**
> 1. I interpreted "Taxonomy" as a *taxonomy/classification of agent patterns* (naming and grouping the patterns). You already have it de facto — see section 1.
> 2. I interpreted "learning models below the concept level" as *learning/training models from beginner level* (small models, fine-tuning, training from scratch). See section 10.

---

## 1. Taxonomy (classification of agent patterns)

**What it is:** a named division of design patterns into groups by the problem they solve. Why: a shared language when designing, and choosing a pattern from a decision tree instead of "by eye".

Existing taxonomies (each looks from a different angle):

| Source | Axis of division | Patterns | Confidence |
|---|---|---|---|
| Anthropic, *Building Effective Agents* | **workflow vs agent**; 5 workflow patterns | prompt chaining, routing, parallelization (sectioning/voting), orchestrator-workers, evaluator-optimizer | High |
| Liu et al., *Agent Design Pattern Catalogue* (arXiv 2405.10467, v1 16.05.2024, v4 06.11.2024) | architecture of FM agents | 18 patterns + "decision model for selecting the patterns" | Medium (abstract only; I did not see the grouping) |
| Brad Murry, 13 patterns (our sources `05`, `09`–`21`) | the main *orchestration problem* | Flow, Authority, Delegation, Reasoning, Planning, Coordination (6 categories × 13 patterns) | High (full text) |
| Huang and Zhou, *Two-Dimensional Framework* (arXiv 2605.13850) | two axes: execution topology × cognitive function | incl. the Anthropic/Google ADK/LangChain catalogues | Low (search-engine snippet only, preprint) |

Quotes (fetched 2026-10-14):
- Anthropic: Workflows = "Systems where LLMs and tools are orchestrated through predefined code paths"; agents: the model itself chooses the steps and tools. Advice: start with the simplest solution, "adding complexity only when it demonstrably improves outcomes". https://www.anthropic.com/engineering/building-effective-agents
- Liu et al.: "a pattern catalogue consisting of 18 architectural patterns" + "decision model". https://arxiv.org/abs/2405.10467

**Conclusion (my interpretation):** you already have a taxonomy — Brad's "Flow/Authority/Delegation/Reasoning/Planning/Coordination" table is a classification *by problem*, while Anthropic's division is a classification *by degree of autonomy* (workflow → agent). It is worth keeping both: first "is this a workflow or an agent?", then "which pattern from the category?".

Sources: [Anthropic](https://www.anthropic.com/engineering/building-effective-agents), [Liu 2024](https://arxiv.org/abs/2405.10467), [2605.13850 (snippet)](https://www.alphaxiv.org/abs/2605.13850.md)

---

## 2. LLM-as-a-Judge

**What it is:** using a model as the evaluator (scoring or A/B comparison) instead of a human/metrics. It scales evaluation where there is no single correct answer.

**Facts (High):** Zheng et al., *Judging LLM-as-a-Judge with MT-Bench and Chatbot Arena* (arXiv 2306.05685, 09.06.2023; v4 24.12.2023):
- strong judges such as GPT-4 reach "over 80% agreement, the same level of agreement between humans";
- named biases: **position bias, verbosity bias, self-enhancement bias**; the judge's limited reasoning ability.
https://arxiv.org/abs/2306.05685

**Facts (Medium/Low):** the survey by Gu et al. (arXiv 2411.15594) divides biases into *task-agnostic* (coming from the LLM itself, hard to remove) and *judgment-specific* (to be limited through task design); a second survey (arXiv 2412.05579) adds, among others, authority and bandwagon bias. Known to me only from search-engine summaries.

**Error-mitigation practices (consistent across sources; part of it is my synthesis):**
- swapping the order of answers and re-evaluating (position bias) — Medium;
- splitting the rubric into small, binary criteria — Low (blog);
- calibration against a set of human labels before you trust the judge — Medium;
- a judge from a *different model family* than the generator (self-enhancement) — **this is my conclusion from the named bias, not a quote**.

**Connection to Brad:** this is exactly his "Quality signal — opinion, not fact" (`07`): an LLM critic = "the weakest signal: it drifts toward 'looks thorough'". His solution: the judge as a *hint*, and the decision is made by a deterministic gate (recompute + diff). This is consistent with the literature.

Sources: [Zheng 2023](https://arxiv.org/abs/2306.05685), [Gu survey](https://arxiv.org/pdf/2411.15594), [survey 2412.05579](https://arxiv.org/pdf/2412.05579)

---

## 3. Reflection

**What it is:** the model evaluates/critiques its own output and corrects it (generate → critique → refine loop). Variants: Self-Refine, Reflexion (reflections go to episodic memory).

**Facts:**
- **Reflexion** (Shinn et al., arXiv 2303.11366, 20.03.2023; v4 10.10.2023): reinforcing agents "not by updating weights" but through verbal reflection stored in episodic memory; claimed "91% pass@1 on HumanEval … surpassing … GPT-4 that achieves 80%". High (abstract, authors' claim).
- **Counterargument:** Huang et al., *Large Language Models Cannot Self-Correct Reasoning Yet* (arXiv 2310.01798, 03.10.2023; v2 14.03.2024): "LLMs struggle to self-correct their responses without external feedback", in some cases quality *drops* after self-correction. High.
- Secondary summaries (Low): the bottleneck is *detecting* the error, not fixing it; gains in papers often depend on external labels/oracles.

**Conclusion (my synthesis):** reflection works when there is an **external signal** (tests, tool result, deterministic verify). Without it, criticism "off the top of the head" can be worthless or harmful. This is exactly the Generator/Critic/Refiner pattern from `17` + the gate from `22`–`25` in Brad (critic + budget `size(drafts) < 4` + `stalled` state).

Sources: [Reflexion](https://arxiv.org/abs/2303.11366), [Huang 2023/24](https://arxiv.org/abs/2310.01798), [Self-Refine](https://arxiv.org/pdf/2303.17651), [Survey 2308.03188](https://arxiv.org/pdf/2308.03188)

---

## 4. Prompt Optimization

**What it is:** automatic tuning of prompts (instructions + few-shot examples) for a metric on a development set, instead of manual "prompt engineering".

**Facts:**
- **GEPA** (Agrawal et al., arXiv 2507.19457, 25.07.2025; v2 14.02.2026; ICLR 2026 Oral): an optimizer based on natural-language reflection and a Pareto front; authors' claim: "outperforms GRPO by 6% on average and by up to 20%, while using up to 35x fewer rollouts"; over 10% above MIPROv2 (e.g. +12% on AIME-2025). High for the *claim*, not independently verified.
- **DSPy:** instructions and demonstrations are program parameters, optimizers (MIPROv2, SIMBA, GEPA) search for values that maximize the metric. Low/Medium (blogs/tutorials).
- **Caveats (Low, secondary):** optimization costs many LLM calls; a prompt can overfit to a badly defined metric; a prompt does not transfer reliably between models.

**Connection:** this is the same loop as Brad's "self-evolving": RUN → MEASURE → DISCOVER → BUILD, "one change at a time", "report the ceiling" (`07`). GEPA does it automatically on prompts; Brad does it on the whole workflow/tools.

Sources: [GEPA](https://arxiv.org/abs/2507.19457), [DSPy overview (blog)](https://founderscreative.substack.com/p/automating-prompt-engineering-with), [agentpatterns.ai](https://agentpatterns.ai/agent-design/dspy-programmatic-prompt-optimization/)

---

## 5. SLM — Small Language Models

**What it is:** small models (usually < ~10 billion parameters according to summaries — the definition is fuzzy) running on consumer hardware, cheap and fast, good at narrow, repetitive tasks.

**Facts (High for the authors' position):** Belcak et al. (NVIDIA Research), *Small Language Models are the Future of Agentic AI* (arXiv 2506.02153, 02.06.2025; version v3 visible with date 22.09.2026): SLMs are "sufficiently powerful, inherently more suitable, and necessarily more economical" for many agentic calls; heterogeneous systems (small + large) are the natural choice where general conversation is needed; the paper gives an algorithm for converting LLM agents→SLM. This is a **position paper** (the authors themselves call it a "value statement" and invite criticism) — not empirical proof.

**Where this fits in practice:** the *routing* pattern in Anthropic — "send simple queries to a smaller, cheaper model and hard ones to a more capable model". That is: SLM for classification/extraction/routing, a large model for hard steps.

**Low:** numbers like "40–70% of queries to be replaced", "10–30× cheaper" come from blogs, not from the abstract — do not use without checking in the PDF.

Sources: [arXiv 2506.02153](https://arxiv.org/abs/2506.02153), [Anthropic – routing](https://www.anthropic.com/engineering/building-effective-agents)

---

## 6. Prompt caching

**What it is:** the provider remembers the processed *prefix* of the prompt; subsequent requests with an identical prefix are cheaper and faster.

**Anthropic (official documentation, fetched 2026-10-14; High):**
- prefix = `tools` → `system` → `messages` up to the block with `cache_control`; a hit requires a **100% identical** prefix; up to 4 explicit breakpoints or an automatic top-level `cache_control`.
- TTL: 5 min (default, refreshed on use) or 1 h (`"ttl": "1h"`).
- prices: 5-min write = **1.25×** the base input price, 1-h write = **2×**, read = **0.1×** (for some models 0.05× / 0.025×).
- token minimums depend on the model (e.g. 512 / 1 024 / 2 048 / 4 096) — below the minimum there is no cache and no error.
- changing tool definitions invalidates the whole cache (tools → system → messages); a timestamp in the cached block = no hits.
- verification: the fields `usage.cache_creation_input_tokens` and `usage.cache_read_input_tokens`.
https://platform.claude.com/docs/en/build-with-claude/prompt-caching

**OpenAI (official documentation, fetched 2026-10-14; High):**
- enabled by default; minimum 1 024 tokens (GPT-5.6+); retention `prompt_cache_options.ttl` = "30m" (older models: in-memory ~5–10 min or 24 h); read **0.1×**, write **1.25×**, "discounted up to 95%"; `usage.input_tokens_details.cached_tokens`.
https://developers.openai.com/api/docs/guides/prompt-caching

**Practice (from the documentation):** put constant things at the start (instructions, tools, documents, examples), variable things at the end; nothing variable in the cached prefix; monitor the hit rate. **Note:** prices/TTL change — treat as the state as of 2026-10-14.

**For a chatbot:** system prompt + store description/FAQ/catalogue = an ideal candidate for a cached prefix.

---

## 7. Long-term memory

**What it is:** information that outlives a single session/context window: facts about the user, history, learned rules.

**Facts:**
- **MemGPT** (Packer et al., arXiv 2310.08560, 12.10.2023): "virtual context management" modeled on the memory hierarchy of operating systems — moving data between "fast and slow" memory, interrupts to control flow. High (abstract).
- **Mem0** (Chhikara et al., arXiv 2504.19413, 28.04.2025): extracts, consolidates and retrieves facts from conversations; the authors' claims on LOCOMO: +26% relative (LLM-as-a-Judge) vs OpenAI, −91% p95 latency and >90% fewer tokens vs full context. **High for the claims, but it is a paper by the product's authors** — not an independent benchmark.
- **Reflexion** keeps reflections in episodic memory (section 3).
- **Typology** semantic (facts) / episodic (experiences) / procedural ("how-to" rules) — Low (blogs: dev.to, forkast.news), but consistent across sources and tools (Letta, Mem0, LangMem according to summaries).

**Connection to Brad:** his "context store" is memory *within a single run* (typed context, journal, replay). Long-term memory across runs is a separate layer; his rule: "if a value must be durable and inspectable, it belongs in context" (`03`).

**Risks (my assessment):** user memory = personal data (GDPR), memory poisoning (OWASP LLM04), need for deletion/"the right to be forgotten".

Sources: [MemGPT](https://arxiv.org/abs/2310.08560), [Mem0](https://arxiv.org/abs/2504.19413), [dev.to guide](https://dev.to/dublecc/how-ai-agent-memory-works-in-2026-context-episodic-semantic-and-procedural-l3f)

---

## 8. Agent loop

**What it is:** the basic loop: *call the model → parse the output → execute tools → append results to the context → repeat*, until a stop condition.

**Facts:**
- **ReAct** (Yao et al., arXiv 2210.03629, 06.10.2022; v3 10.03.2023): interleaving reasoning traces and actions; "ReAct overcomes issues of hallucination and error propagation prevalent in chain-of-thought reasoning". High.
- **Anthropic:** agents operate in a tool-use loop driven by feedback from the environment; "Stopping conditions, such as a maximum iteration count" keep control; autonomy = higher cost and risk of compounding errors. High.
- **Stop conditions (Low, blogs, consistent):** goal reached, iteration limit, token/cost budget, consecutive tool errors, cancellation by the user; an append-only log of every step for replay.

**Two control styles (comparison with our research):**
| | Model loop (ReAct) | Machine loop (Brad: reactive reducer) |
|---|---|---|
| Who decides the next step | the model | declared edges + guards |
| Stop | the model "thinks it is done" + limits | terminal state, `maxIterations`, cap of 128 micro-steps, no-progress guard |
| Strength | flexibility on open-ended tasks | determinism, replay, audit |

Brad also warns that the no-progress guard works only *within a single* `advance()`, not across turns (`13`, `14`, `16`) — cross-turn loops are guarded by the verdict enum + `maxIterations`.

Sources: [ReAct](https://arxiv.org/abs/2210.03629), [Anthropic](https://www.anthropic.com/engineering/building-effective-agents)

---

## 9. Your own chatbot on the website (how to lay it out)

**Minimal architecture (my synthesis of the sources below):**

```
Browser (widget: HTML/JS, SSE)
        │  HTTPS, no keys at all
        ▼
Your backend (proxy)  ──►  limits: per-IP/user + cost budget
   │   │   │                input validation, logging
   │   │   └──► memory/session (database)
   │   └──────► RAG: search in site content/FAQ/catalogue
   ▼
Model: (A) provider API  or  (B) own SLM (Ollama/llama.cpp/vLLM)  or  (C) router A+B
```

Key rules (Medium/Low — security blogs; authoritative base: OWASP):
1. **API key only on the server side**, never in JS or in the repo; all LLM calls through the backend proxy.
2. **Rate limit counts requests, not cost** — add a per-user/day budget and `max_tokens`.
3. **A rate limit alone will not stop prompt injection** — separate system instructions from user content, treat content fetched from pages as untrusted (indirect injection), limit tool permissions.
4. **Assume the system prompt will leak** — do not put secrets in it.
5. **OWASP Top 10 for LLM Applications 2025** (fetched 2026-10-14, High): LLM01 Prompt Injection, LLM02 Sensitive Information Disclosure, LLM03 Supply Chain, LLM04 Data and Model Poisoning, LLM05 Improper Output Handling, LLM06 Excessive Agency, LLM07 System Prompt Leakage, LLM08 Vector and Embedding Weaknesses, LLM09 Misinformation, LLM10 Unbounded Consumption. https://genai.owasp.org/llm-top-10/
6. **Streaming:** the response via SSE (`stream: true`) gives smooth token display; you have to handle an error in the middle of the stream. https://platform.claude.com/docs/en/build-with-claude/streaming
7. **Cache the prefix** (section 6) and **measure** the hit rate.
8. **Evaluation:** a set of test questions + an LLM judge calibrated on manual labels (section 2) + deterministic checks wherever possible (e.g. "does the answer contain an existing SKU/URL").

**API vs own model (Low — comparison blogs):**
- *Provider API:* fastest start, best quality, per-token cost, data leaves your infrastructure.
- *Own SLM:* control and privacy, low cost at high traffic, but maintenance and weaker general quality. According to summaries: **Ollama** — easiest for a single user, **llama.cpp** — the most control/quantization, **vLLM** — wins with many concurrent users (needs a GPU). Tokens/s numbers from blogs conflict — benchmark on your own hardware.
- *Hybrid (router):* a small model for classification/FAQ, a large one for hard questions (see SLM + routing).

**Off-the-shelf options (Low):** Open WebUI (self-hosted UI for Ollama/API with RAG), WordPress plugins such as "AI Chatbot". I did not check their quality or licenses.

**Relation to your ECommerceApp (suggestion only):** a store chatbot = RAG over the catalogue/FAQ/returns policy, with no permission to modify orders (LLM06 Excessive Agency). Actions (e.g. order status) only through existing, authorized endpoints.

---

## 10. Learning models "from level zero" (a ladder)

From the cheapest and safest to the most "own":

1. **Prompt + context** (zero training) — start here; Anthropic: "simplest solution first".
2. **RAG** — knowledge from your documents in the prompt. According to tutorials, fine-tuning teaches *new facts* poorly; it serves rather style/format/behavior (Low, blogs).
3. **Fine-tuning an SLM with LoRA/QLoRA.** LoRA freezes the base weights and trains small low-rank adapter matrices of rank `r`; after training the adapter can be merged (`merge_and_unload()`). High: official Hugging Face PEFT documentation. According to tutorials (Low) a 7B can be tuned on a single ~24 GB card with quantization. https://huggingface.co/docs/peft/main/en/conceptual_guides/lora
4. **Training a tiny model from scratch — to understand how it works:**
   - **nanoGPT** (Karpathy): the README marks the repo as deprecated (November 2025 update, pointing to nanochat), but the quick start still shows training a character-level GPT on Shakespeare: about 3 min on a single A100 or about 3 min on a laptop with a smaller model (loss 1.88 — weaker samples). https://github.com/karpathy/nanoGPT
   - **nanochat** (Karpathy): "The best ChatGPT that $100 can buy"; README: a model with GPT-2 capabilities for "$48 (~2 hours of 8XH100 GPU node)", about $15 on spot; the full pipeline (tokenization → pretraining → finetuning → eval → inference); "talking to a kindergartener". CPU/MPS only in a scaled-down version, "You will not get strong results". https://github.com/karpathy/nanochat

**A question for you:** if "below the concept level" meant something else (e.g. *from zero for a layperson*, *without a GPU*, *a model without internet*), tell me — I will adjust.

---

## 11. How it all fits together (synthesis, mine)

- **Taxonomy** (1) helps *choose* a pattern; the **agent loop** (8) is its engine; **reflection** (3) and **LLM-as-judge** (2) are quality mechanisms — both need an *external signal* or a calibrated judge.
- **Prompt optimization** (4) and **SLM** (5) are cost/quality levers; **prompt caching** (6) is a cost lever without changing quality; **memory** (7) is a lever for continuity between sessions.
- A common thread of all of Brad's sources and the literature: *the model contributes value, while control, limits and verification sit in a deterministic shell.*

## 12. Gaps and caveats

- I did not open full papers — only abstracts and official pages (numbers "claimed by the authors" ≠ independent benchmarks).
- Taxonomy 2605.13850, the LLM-as-judge surveys (2411.15594, 2412.05579) and Self-Refine are from search-engine snippets only.
- Prices, token minimums and cache TTL are the state from the documentation as of 2026-10-14; they change often.
- Comparisons of Ollama/llama.cpp/vLLM and chatbot security advice come from blogs (some commercial) — to be confirmed in your own test and in OWASP.
- No own experiments: no number in this document has been reproduced.

## 13. Full list of sources (with dates)

| Source | Type | Date | Used in |
|---|---|---|---|
| [Anthropic – Building Effective Agents](https://www.anthropic.com/engineering/building-effective-agents) | official blog | fetched 2026-10-14 | 1, 5, 8, 11 |
| [Liu et al., Agent Design Pattern Catalogue](https://arxiv.org/abs/2405.10467) | paper | 2024-05-16 (v4 2024-11-06) | 1 |
| [Zheng et al., MT-Bench / LLM-as-a-Judge](https://arxiv.org/abs/2306.05685) | paper | 2023-06-09 (v4 2023-12-24) | 2 |
| [Gu et al., Survey on LLM-as-a-Judge](https://arxiv.org/pdf/2411.15594) | survey (snippet) | — | 2 |
| [Reflexion](https://arxiv.org/abs/2303.11366) | paper | 2023-03-20 (v4 2023-10-10) | 3, 7 |
| [Huang et al., LLMs Cannot Self-Correct Reasoning Yet](https://arxiv.org/abs/2310.01798) | paper | 2023-10-03 (v2 2024-03-14) | 3 |
| [GEPA](https://arxiv.org/abs/2507.19457) | paper (ICLR 2026 Oral) | 2025-07-25 (v2 2026-02-14) | 4 |
| [SLM are the Future of Agentic AI](https://arxiv.org/abs/2506.02153) | position paper | 2025-06-02 (v3 2026-09-22) | 5 |
| [Anthropic – Prompt caching docs](https://platform.claude.com/docs/en/build-with-claude/prompt-caching) | documentation | fetched 2026-10-14 | 6 |
| [OpenAI – Prompt caching docs](https://developers.openai.com/api/docs/guides/prompt-caching) | documentation | fetched 2026-10-14 | 6 |
| [MemGPT](https://arxiv.org/abs/2310.08560) | paper | 2023-10-12 (v2 2024-02-12) | 7 |
| [Mem0](https://arxiv.org/abs/2504.19413) | paper | 2025-04-28 | 7 |
| [ReAct](https://arxiv.org/abs/2210.03629) | paper | 2022-10-06 (v3 2023-03-10) | 8 |
| [OWASP Top 10 for LLM Apps 2025](https://genai.owasp.org/llm-top-10/) | standard | fetched 2026-10-14 | 9 |
| [Anthropic – Streaming](https://platform.claude.com/docs/en/build-with-claude/streaming) | documentation | fetched 2026-10-14 | 9 |
| [HF PEFT – LoRA](https://huggingface.co/docs/peft/main/en/conceptual_guides/lora) | documentation | fetched 2026-10-14 | 10 |
| [karpathy/nanoGPT](https://github.com/karpathy/nanoGPT) | README | fetched 2026-10-14 | 10 |
| [karpathy/nanochat](https://github.com/karpathy/nanochat) | README | fetched 2026-10-14 | 10 |
| Blogs/comparisons (dev.to, forkast.news, morphllm, insiderllm, reintech, enterno, webreference, LoRA tutorials) | secondary | various | marked "Low" |
