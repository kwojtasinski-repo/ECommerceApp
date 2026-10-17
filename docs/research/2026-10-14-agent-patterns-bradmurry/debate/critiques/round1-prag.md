# Debater: prag, Round 1, critique

Abbreviations: 00 = analysis/00-summary.md, 01 = analysis/01-cross-source-verification.md, LSR = 2026-10-14-llm-self-research/README.md. Opened and checked: 00, 01, LSR (sections 2, 3, 6, 7, 9, 12) and the repo code.

## Critique of PRO

**P1. pro#1 "The direction is independently confirmed by Zheng 2023 and Huang 2023" as support for the reducer.** Shift: Zheng concerns judge biases, Huang self-correction. Neither concerns "LLM as a state machine". 01 §4 D5 rates this as "Partially supported" and "consistent... at the level of direction (not literally)". Pro notes this in the rating, but uses these papers as justification for an expensive mechanism. For 1-3 devs an enum + `switch` in C# is enough, with no reducer at all. Verdict: over-interpretation, direction supported.

**P2. pro#5 "The repo already has Outbox/Inbox and sagas, so this is simply a continuation".** I checked the repo: Outbox/Inbox exist (`Messaging/IOutboxWriter`, `OutboxCleanupTask`, `InboxCleanupTask`, `IProcessedMessageGuard`), sagas too (`Application/Sagas/ISagaDefinition`, `RefundSagaDefinition`, `CartRecoverySagaDefinition`). However, a grep for `Anthropic|OpenAI|Chatbot|IChatClient|SemanticKernel` in `*.cs`/`*.csproj` returned nothing. This is a deterministic shell for domain events, not for LLM calls. What is missing is an idempotency key for a chat turn, an agent run journal, an iteration limit. Therefore the "we already have it" thesis is accurate only in the sense of discipline and ready-made building blocks (idempotent consumer, DLQ), not a ready-made shell for an agent. This also applies to my prag position ("you already have this") and here I **partially concede**: the wording was too strong. Correction: for a read-only chatbot the shell is not needed, and for actions with an effect (refund) it is better to hook up the existing `RefundSaga` than to build a new journal.

**P3. pro#4 "STRONG" for Generator/Critic/Refiner (pattern 9).** What is STRONG is the "external signal" principle. Brad's pattern 9 itself has a limitation (the refiner re-emits the whole artifact, 00 §B). And for a chatbot, checking the SKU/price is an ordinary post-validator, not a Critic/Refiner loop. The pattern label is unnecessary here and raises the apparent implementation cost. Pro#4 also omits that Reflexion 91% is the authors' claim (pro itself points this out), so "STRONG" is inflated for the whole.

**P4. pro#3 router as an enum "STRONG as to the pattern".** The support is Anthropic routing [LSR#1,#5], agreed. But LSR §12 itself caveats that sections 1 and 5 partly rely on snippets and that there was no cross-verification. Brad's pattern 3, in turn, has a documented bug ("routing twice... does nothing", 00 §B), so copying his variant without his runtime makes no sense. To implement: the router in code, without a reference to pattern 3.

## Critique of CON

**C1. con#1 "In the repo we already have Outbox/Inbox and a saga engine, so the deterministic shell is our own code" (as an argument against).** The same error as mine, see P2. Correctly: this is discipline and building blocks, not a solution for LLM loops. Con is right that Aegis must not be imported. However, the conclusion "we build nothing" does not follow, because iteration limits, an idempotent turn key and an agent run log still have to be added (01 C1: the guard does not span turns).

**C2. con#3 the author's self-correction (C1) as proof of weakness.** Checked in 01: C1 is real (intro "bounded by... no-progress guard" vs "does not span turns"). Supported. But con uses it against the pattern in general, whereas for a chatbot the conclusion is the opposite: since the guard does not span turns, the turn limit must be in the ASP.NET code. That is an argument for implementing the limit, not against. Con's conclusion: overly general.

**C3. con#6 AutoGen C9-C11.** Supported in 01 (C7-C12 really are prose/code discrepancies, C10 quote about "not meant to be used in real applications"). Understatement: con itself writes "MEDIUM", but omits that 01 §5 warns of 5.2% mismatched quotes in the findings, so the literalness of these quotes is also "to be verified". It does not change the conclusion (SKIP), but the argument is no more certain than the rest.

**C4. con#7 "Risk evidence: STRONG (OWASP)" for memory.** Misattribution: LSR §7 gives this as "Risks (my assessment)", and OWASP LLM04 concerns data/model poisoning, not directly conversation memory. Con notes "my assessment, not the source", but then labels it STRONG. Properly: the risk is probable (GDPR), the evidence MEDIUM. The DEFER conclusion remains valid.

**C5. con#8 "without an evaluation set there is nothing to optimize".** Agreed, but con gives no criterion for "when". The lack of promotion thresholds is a practical gap (cf. my section c).

## Points of agreement (consensus candidates)
1. First slice: proxy, key on the server side, `max_tokens`, cost budget, prompt caching (pro#6/7, con "what I concede", prag ADOPT).
2. Loop limits outside the model and an external, deterministic signal instead of an LLM judge (pro#2/#4, con#5, prag).
3. No Aegis as a dependency, no debate/quorum/long-term memory/self-evolution at the start (pro "What I do not defend", con, prag SKIP/DEFER).

## Still disputed
1. Whether Brad's patterns (3, 8, 9) are needed as named patterns, or plain C# is enough (pro: yes, as a vocabulary; con and prag: code without labels).
2. Whether actions with an effect (refund) may ever be handed to the agent: pro allows it after idempotency/journal, con rules it out, prag: only through the existing saga/endpoint, after a read-only pilot.
