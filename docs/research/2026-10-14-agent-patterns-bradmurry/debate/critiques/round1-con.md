# Debater: con, Round 1, critique

Verified in: 00-summary.md, 01-cross-source-verification.md, findings/bm-series-parts-raw, bm-patterns-8-13-raw, bm-agentic-core-raw, bm-agents-fsm, llm-self-research/README.md.

## Critique of pro

**P1 (pro #1)**: "The direction is independently confirmed by Zheng 2023 and Huang 2023", rated STRONG as to direction.
Zheng and Huang are about judging and self-correction, not about steering flow through a state machine. 01#4 D5 rates Brad's sentence about the LLM as a state machine as "Partially supported", consistent "at the level of direction (not literally)". The STRONG rating is therefore inflated. The "order confirmation without verification" example is pro's idea, not from the sources.

**P2 (pro #4)**: "Zheng, Huang and Brad agree that the judge is weak".
The attribution is distorted. README#2 quotes Zheng: strong judges reach "over 80% agreement", i.e. as much as humans, with named biases. That is not a "weak judge". Huang concerns self-correction without an external signal, not judging. "Weakest signal" is only from Brad (01#2 nonetheless classes it as directional High). Pro merges three different theses into one.

**P3 (pro #2)**: loop limits = STRONG, with "cap 128", "size(drafts)<4", "stalled".
The numbers hold up at the quote level: 128 is literally in bm-agentic-core B4, and `size(drafts) < 4` and `stalled` in bm-patterns-8-13. The first pass (bm-agents-fsm) had 128 as Low, but it was superseded. The STRONG rating is nonetheless inflated: 01#2 says independence is "partial", and ReAct is "blogs". The 128 and `drafts<4` mechanisms are specific to the reducer, which pro itself rejects. For a chatbot a `for` loop with a counter is enough.

**P4 (pro, ordering)**: "week 1", "weeks 2-3".
The schedule does not come from any source. Pro does not label it as its own proposal (prag does). "Pattern 9 with an external signal" is actually the SKU/URL check from README#9 point 8, not Brad's mechanism. Brad has the recompute+diff gate (README#3).

Pro survives the check in part: caching (README#6) and UNKNOWN-not-FAILED (bm-series-parts A6) are quoted correctly, and pro honestly rates #5 and #8 as WEAK/MEDIUM.

## Critique of prag

**G1 (prag, ADOPT)**: loop limit, "consistent: Anthropic, ReAct, Brad. STRONG".
README#8 quotes ReAct on hallucinations and error propagation, not on limits. The limits come from Anthropic. 01#2 says "partially" independent. ReAct was added without grounding in a source.

**G2 (prag, tiering)**: "Deterministic shell" and "RAG over the catalog" in ADOPT NOW with a MEDIUM rating.
This is inconsistent with its own rule (value × evidence). LLM-as-judge has the same MEDIUM rating, yet goes to PILOT. Brad's shell is a single-author claim (01#2: no independent correlation). RAG relies on README#9, which itself says "my synthesis", from blogs. "You already have this" comes from the repo context, not from the sources.

**G3 (prag, SKIP)**: patterns 6, 10, 12, 13, justification "Low (no evidence of effectiveness)" [01#2].
01#2 applies "Low" exclusively to debate. Auction (10) and dynamic team (13) have no effectiveness rating there at all. The rejection is reasonable, but the quote does not support it.

**G4 (prag, PILOT)**: "Short session memory" has no cited source.

**Self-critique (concession)**: my thesis 6 generalizes "not meant to be used in real applications". According to 01 C10 this applies to the group chat page, not the whole of AutoGen. I withdraw the generalization; the prose-code discrepancies C7-C12 remain.

## Agreement (consensus candidates)
1. Backend proxy, key on the server side, cost budget and `max_tokens`, no write permissions (OWASP LLM06/LLM10).
2. Prompt caching of the fixed prefix (README#6, documentation).
3. Reflection and judge only with an external signal or calibrated on human labels; debate, quorum, auction and long-term memory deferred.

## Disputed
1. Whether "Brad's deterministic shell" (idempotency, journal) belongs in ADOPT, given that the evidence is single-author and concerns actions the chatbot does not have in the MVP.
2. Whether a STRONG rating is deserved for "directional" agreement, when the independent sources (Zheng, Huang) concern a different issue than the claim.
