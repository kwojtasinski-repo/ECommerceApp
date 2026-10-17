# Debater: con, Round 1, position

Perspective: skeptic. Evidence scale: STRONG = primary/independent source, MEDIUM = one explicit source, WEAK = one author's self-description.

## Theses

1. **The "Aegis" runtime / reactive reducer is a one-person stack without verification.** All claims about the runtime come from one author, and the material contains no publicly verified implementation [00-summary#Gaps]. The correlation "determinism, replay, bounded cost" belongs only to Brad, "several posts are not independent confirmation" [01#2]. The thesis "the glue code is gone" has the verdict Unsupported (D1) [01#4]. Evidence: WEAK. Importing the FSM DSL (CEL, microsteps, cap 128) into .NET is a cost without guarantees. In the repo we already have Outbox/Inbox and a saga engine, so the "deterministic shell" is our own code.

2. **The "baseline vs optimum" metrics are not evidence.** They are self-descriptions without trials and repetitions, and it is not known whether they are real runs [00-summary#Gaps]. In one example the author calls a value higher than after Round 2 the "optimum" (41 000 vs 38 000 tokens, C5) [01#1]. The final FSM in the entity resolution example has no `policy` block, although the series teaches that this is a safeguard (C6). Evidence: WEAK.

3. **The author himself weakens his guarantees.** The series intro promises that the loop is bounded by, among others, a no-progress guard. Later posts correct this: the guard "does not span turns", leaving two safeguards instead of three (C1) [01#1]. The intro was not corrected. For a chatbot, i.e. a conversation made of many turns, this is exactly the gap that concerns us most. Evidence: MEDIUM (explicit self-correction).

4. **Debate and quorum: no evidence of effectiveness, and the cost is certain.** The collected material contains no data on the effectiveness of debate (correlation: "Low") [01#2]. The only AutoGen example ends in full agreement [00-summary#C]. Brad himself points out that shared blind spots require model diversity, not a different loop, and that quorum has error correlation "not fixable by mechanism" [00-summary#B, patterns 6 and 12]. The cost is debaters × rounds × (positions + critiques) + judge. Several copies of the same model voting by majority reproduce the same error. Evidence against: MEDIUM (author's admission). The external paper on debate (arXiv 2406.11776) was not fetched [01#2].

5. **Reflection and LLM-as-judge without an external signal are risky.** This is the best-supported point, but it supports skepticism: Huang et al. — "struggle to self-correct… without external feedback", and quality sometimes drops [self-research#3]. Zheng et al. name position, verbosity and self-enhancement bias [self-research#2]. Brad calls the LLM critic "the weakest signal" [01#2]. Conclusion: the judge is useful only when calibrated on human labels. A Generator/Critic/Refiner loop on chatbot answers without verification (e.g. whether the SKU exists) adds cost and latency without benefit. Evidence: STRONG (3 independent groups).

6. **AutoGen: prose and code diverge, and the page itself warns.** Reflection: the prose mentions "max iterations", the code has no limit, so an infinite loop is possible (C9). Group chat: the page says it is not "meant to be used in real applications" (C10). Debate: neighborhood and message type in the prose ≠ code (C7, C8). Mixture-of-agents: 3 layers in the prose, 2 in the code (C11) [01#1]. This is educational material, not a production pattern. Evidence: MEDIUM, but directly verifiable in the code.

7. **Long-term memory is a legal and security liability before it becomes a feature.** The Mem0 claims (+26%, −91% latency) come from a paper by the product's authors, not from an independent benchmark [self-research#7]. Risks (my assessment, not the source): personal data and GDPR, memory poisoning (OWASP LLM04), right to erasure. A store chatbot does not need to know "who you are" between sessions in order to answer a question about returns. Evidence of benefit: WEAK. Evidence of risk: STRONG (OWASP).

8. **SLM and prompt optimization: promising, but not at the start.** The SLM is a position paper that the authors themselves call a "value statement"; figures like "10–30× cheaper" come from blogs (Low) [01#4 D6; self-research#5]. GEPA is the authors' claims without independent verification, and the optimization costs many calls and may overfit to a wrong metric [self-research#4]. Without an evaluation set there is nothing to optimize. Evidence: MEDIUM/WEAK.

Additionally: Mastra and Medium yielded 0 facts, so their patterns do not enter the catalog [00-summary#Gaps]. The self-research was not cross-verified and no number was reproduced [self-research#12].

## What I concede is well supported
Prompt caching (official documentation, 0.1× read) [self-research#6], loop limits outside the model (Anthropic + Brad), the "simplest first" principle (Anthropic; Brad: "a for-loop could do for free") [01#2], the OWASP Top 10 LLM and limiting permissions (LLM06).

## What I would NOT build
- Brad's reducer/FSM runtime, debate, quorum, auction, dynamic team, swarm.
- Reflection loops without an external signal and a self-evolving agent.
- Long-term memory about the user; one's own/fine-tuned SLM; automatic prompt optimization.

## What I would build instead
A single model call with RAG over the FAQ/catalog/returns policy, prompt chaining or routing as plain C# code. API key only on the server side, cost limits and `max_tokens`, a hard iteration limit. Actions (e.g. order status) only through existing authorized endpoints, with session data and no persistent memory. System prefix cache. A small test set with deterministic checks (SKU/URL exists). Order: caching and guardrails, then eval, and only then the judge, router or SLM, if measurement justifies it.
