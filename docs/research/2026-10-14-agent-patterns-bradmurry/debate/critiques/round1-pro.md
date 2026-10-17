# Debater: pro, Round 1, critique

Abbreviations: 00 = analysis/00-summary.md, 01 = analysis/01-cross-source-verification.md, LSR = llm-self-research/README.md. Opened and checked: 00, 01, LSR, bm-patterns-1-7-raw (F5c/F6g), bm-patterns-8-13-raw (F8.9), autogen-raw-9pages, bm-series-parts-raw.

## Critique of con

1. **con#1** ("the Aegis runtime is a one-person stack without verification... cost without guarantees"). Fact supported: 00#Gaps and 01 D1 (Unsupported) confirm a single source. Weakness: con flattens two things. I am not defending the runtime, only the principle "limits and validation outside the model", which con itself admits at the end ("loop limits outside the model"). Thesis 1 and the "I concede" section are therefore partly contradictory.
2. **con#3** (the no-progress guard does not span turns, the intro was not corrected). **Supported**: 01 C1 and bm-patterns-1-7 F5c ("it does not span turns"). But the conclusion "this is the gap that concerns us most" is con's interpretation. The source says turns are guarded by an enum + `maxIterations`, and our code is to hold that itself. That is an argument for a limit in our code, not against the pattern.
3. **con#7** (memory risk: "STRONG (OWASP)"). **Overstated.** LSR#7 marks "Risks" as "my assessment", and OWASP LLM04 concerns data and model poisoning, not user memory directly. The OWASP list is STRONG, its application to memory is MEDIUM.
4. **con#6** (AutoGen: prose/code discrepancies). **Supported**: autogen-raw line 549 ("no max iterations in code"), line 199 ("not meant to be used in real applications"). Full agreement.

## Critique of prag

5. **prag ADOPT, loop limit** ("consistent: Anthropic, ReAct, Brad"). **Wrongly attributed.** 01#2 gives "blogs about ReAct", and LSR#8 quotes the stop conditions from Anthropic; the ReAct abstract does not contain them. Two sources, not three.
6. **prag ADOPT, RAG + SKU/URL checks = "MEDIUM [README#9]"**. Chapter 9 is marked "Medium/Low — blogs", and the quote about SKU/URL is point 8 of that chapter, not evidence. The MEDIUM rating is generous, but prag rightly places it in the ADOPT tier as a practice, not a research result.
7. **prag ADOPT, "Zheng >80%" as the promotion threshold**. Prag notes that the threshold is its proposal. LSR#2 speaks of ">80%" for strong judges (GPT-4) on MT-Bench, not for a store dataset. Honest labeling, but the threshold transfers poorly.
8. **prag ADOPT, idempotency/journal "MEDIUM [00#A]"**. This also hits my claim 5, so **I admit my own error**: I wrote "no automatic retry on an unknown outcome". The source (bm-series-parts A6) says the opposite: "back off and try again with the SAME key". Correction: retry with the same idempotency key, with no new effect.

## Agreement (consensus candidates)
- Prompt caching of the prefix, backend proxy and cost limits, no write permissions (LSR#6, #9) as step 1.
- Debate, quorum, auctions, dynamic team, AutoGen as production code: SKIP (01#2 "Low", no effectiveness data).
- Reflection/judge only with an external signal or calibrated on human labels (Zheng, Huang; 3 independent groups).

## Disputed issues
- Whether the "deterministic shell" in our repo is a requirement for new work (my claim 5) or the already existing Outbox/Saga (prag), and whether a read-only chatbot needs a journal in v1 at all?
- When to introduce the intent router as an enum: right away (me) or only after measurement (con: "one call + RAG")? No source measures the router's gain in a store chatbot.
