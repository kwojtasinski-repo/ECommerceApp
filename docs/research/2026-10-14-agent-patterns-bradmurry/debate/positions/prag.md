# Debater: prag, Round 1, position

Perspective: a team of 1-3 devs, an existing .NET application (Outbox/Inbox, saga, order/payment flows). Ranking = (value x strength of evidence) / (cost x risk). Evidence scale: STRONG = independent/primary, MEDIUM = one explicit source, WEAK = one author's self-description.

## (a) Tiers

**ADOPT NOW**
- **Backend proxy + key only on the server + limits (requests and cost budget, `max_tokens`)** - OWASP LLM10 Unbounded Consumption. STRONG [2026-10-14-llm-self-research/README.md#9].
- **The "simplest first" principle: workflow instead of agent** - Anthropic and Brad agree: "a for-loop could do for free". STRONG [01-cross-source-verification.md#2].
- **No write permissions to orders (LLM06 Excessive Agency)**; order status only through existing, authorized endpoints. STRONG [README#9].
- **Explicit loop limit outside the model** (max iterations, token budget) - consistent: Anthropic, ReAct, Brad. STRONG directionally [01#2].
- **Prompt caching of the fixed prefix** (system prompt + FAQ + policies): read at 0.1x the input price, write at 1.25x. STRONG (vendor documentation, prices as of 2026-10-14) [README#6].
- **RAG over catalog/FAQ/returns + deterministic checks** (whether the SKU/URL exists). MEDIUM [README#9].
- **Deterministic shell around the model** (idempotency, journal, timeout = UNKNOWN, DLQ): you already have this in Outbox/Inbox/saga - Brad's pattern is here only a confirmation, not a new cost. MEDIUM [00-summary.md#A].

**PILOT**
- **LLM-as-judge calibrated on human labels** - Zheng: ">80% agreement with humans", but position/verbosity/self-enhancement bias. STRONG as to biases, MEDIUM as to practices [README#2].
- **Routing to a small model (SLM/cheaper tier)** for FAQ/intent classification - Anthropic routing; the SLM paper itself is a position paper. MEDIUM-WEAK [README#5].
- **Reflection only with an external signal** (e.g. a link/SKU validator) - Huang: without feedback it is sometimes worse. STRONG warning [README#3].
- **Short session memory** (no long-term memory, no personal data).

**DEFER**
- **Long-term user memory** (Mem0/MemGPT) - GDPR, memory poisoning; Mem0's numbers come from the paper of the product's authors. WEAK-MEDIUM [README#7].
- **Automatic prompt optimization (GEPA/DSPy)** - the authors' claims, no independent verification; an evaluation set is needed first. MEDIUM [README#4].
- **Own SLM hosting / LoRA fine-tuning** - the maintenance cost at low traffic has no justification. WEAK.

**SKIP**
- **Multi-agent debate, quorum, auctions, dynamic teams (patterns 6, 10, 12, 13)** - "Low (no evidence of effectiveness)" [01#2]; cost = debaters x rounds; a store chatbot does not need it.
- **Reactive reducer runtime (Aegis) as a dependency** - one author, no public, verified implementation [00-summary.md#Gaps]; the baseline->optimum metrics are WEAK (inconsistency C5).
- **Self-evolving loop in production** - see above; Mastra/Medium without content, they do not enter at all.
- **AutoGen patterns as production code** - the page itself: "not meant to be used in real applications" (C10), numerous prose/code discrepancies (C7-C12).

## (b) Minimal chatbot architecture

1. Widget (JS, SSE) -> ASP.NET endpoint (proxy). Key and prompts only on the server side.
2. Limits: per IP/session/day, daily cost budget with a kill-switch, `max_tokens`, turn limit.
3. RAG: index of catalog, FAQ, policies; retrieved content = untrusted (indirect injection); assume the system prompt leaks.
4. Prompt: fixed prefix (cache), variables at the end; monitor `cache_read_input_tokens`.
5. Data access: read-only through existing, authorized queries (order status after user verification); zero write commands. Escalation to a human as the terminal state.
6. Conversation logging (without excessive personal data), support for deletion.
7. Evaluation: a question set (start with ~50-100 from real tickets - this is my proposal, not from the sources), deterministic checks, the judge as an auxiliary signal, calibrated on human labels; swapping the answer order; a judge from a different family than the generator (a conclusion, not a quote) [README#2].

## (c) Promotion criteria PILOT -> ADOPT

The thresholds are my proposals for calibration, not data from the sources.
- **Judge:** agreement with human labels on our own set >= approx. 80% (Zheng's reference point) and stability after swapping the order; otherwise it stays only a signal.
- **Routing/SLM:** a drop in cost per conversation without a drop in quality on the evaluation set beyond a set margin, measured on our own traffic.
- **Reflection:** improvement on a set with an external validator, without an increase in latency beyond budget.
- **Session memory:** no privacy incidents and approval of the legal basis.
- Every promotion: one change at a time (Brad's discipline), measured before/after on a fixed set, with a rollback.
