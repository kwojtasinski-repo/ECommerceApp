# Decision: agent patterns for ECommerceApp (website chatbot)

Sources of the decision: debate/positions/{pro,con,prag}.md, debate/critiques/round1-*.md, judge-round1.md.
Evidence scale: STRONG means a primary or independent source (several groups). MEDIUM means one explicit source. WEAK means one author's self-description or blogs.
All threshold numbers below are marked as a **proposal**. None comes from the sources.

## (a) Tiers

### ADOPT NOW
| Element | Reason (1 line) | Evidence strength |
|---|---|---|
| ASP.NET backend proxy, API key only on the server | Prevents key leakage and uncontrolled use (OWASP LLM10) | STRONG (OWASP, README §9 pt 5) |
| Limits: `max_tokens`, turn limit, cost budget with a kill-switch, rate limit per session/IP | Unbounded Consumption (LLM10). A turn limit is necessary, because Brad's guard "does not span turns" (01 C1) | STRONG for the risk list; the specific values are a proposal |
| No write tools; reads (order status) exclusively through existing authorized endpoints | Excessive Agency (LLM06) | STRONG (OWASP) |
| Simplest first: a workflow in C# code instead of an autonomous agent | Anthropic and Brad independently ("a for-loop could do for free") | STRONG, 01 §2 High |
| Explicit iteration limit and stop outside the model | Anthropic ("Stopping conditions, such as a maximum iteration count") and Brad | STRONG directionally (01 §2: independence "partial"; ReAct is **not** a source of limits) |
| Prompt caching of the fixed prefix (system prompt, FAQ, policies), cache-read monitoring | Vendor documentation: read 0.1×, write 1.25× / 2× | STRONG (official documentation, as of 2026-10-14) |
| RAG over the catalog, FAQ and returns policies; RAG content treated as untrusted | Store knowledge without fine-tuning; risk of indirect prompt injection (LLM01) | MEDIUM/WEAK as a practice (README §9 is "my synthesis", blogs); OWASP STRONG |
| Deterministic post-validators: whether the SKU, URL and price exist and match the database | Limits hallucinated products and prices (LLM09); an external signal instead of self-critique | STRONG for the "external signal" principle (Huang; 01 §2). The mechanism itself is a practice (README §9 pt 8, WEAK) |

### PILOT
| Element | Reason | Evidence strength |
|---|---|---|
| Evaluation set from real tickets + deterministic checks | Without it nothing can be promoted from PILOT or DEFER | MEDIUM (README §2, §9 pt 8) |
| LLM-as-judge as an auxiliary signal, calibrated on human labels, with answer-order swapping | Zheng: "over 80% agreement" on MT-Bench, but with position, verbosity and self-enhancement biases | STRONG as to biases; MEDIUM as to portability to a store |
| Routing to a cheaper model or SLM for FAQ and intent classification | Anthropic's routing pattern; the SLM paper is a position paper ("value statement") | MEDIUM/WEAK (01 D6) |
| Reflection exclusively with an external validator | Huang: without external feedback quality is sometimes worse | STRONG as a warning; effectiveness in a store unknown |
| Short session memory (no personal data, no persistence between sessions) | Conversation continuity | no source (prag's conclusion, con's G4) |

### DEFER
| Element | Reason | Evidence strength |
|---|---|---|
| Long-term user memory (Mem0, MemGPT) | Mem0's numbers come from the paper of the product's authors; GDPR; poisoning risk | benefit WEAK; risk MEDIUM (README §7 "my assessment", **not** STRONG) |
| Automatic prompt optimization (GEPA, DSPy) | The authors' claims; an evaluation set and a metric must exist first | MEDIUM/WEAK (README §4) |
| Own SLM hosting, LoRA fine-tuning | Maintenance cost at low traffic with no evidence of benefit | WEAK |
| Actions with an effect (refund, cancellation) performed by the chatbot | See §c, disputed point 3 | idempotency/retry: classic pattern; transfer to an agent only at Brad (WEAK/MEDIUM) |
| Self-evolving loop (RUN/MEASURE/DISCOVER/BUILD) | Brad's metrics are a self-description without trials (01 §2 Low–Medium, C5) | WEAK |

### SKIP
| Element | Reason | Evidence strength |
|---|---|---|
| The reactive reducer runtime "Aegis" as a dependency | One author, no public, verified implementation; "the glue code is gone" = Unsupported | 00 Gaps, 01 D1 |
| Multi-agent debate (6) | No effectiveness data in the material (01 §2 Low); cost grows with the number of debaters and rounds | Low |
| Quorum (12), auction (10), dynamic team (13), swarm (5) | A store chatbot does not need them. For quorum Brad himself points to correlated errors. For 10 and 13 there is **no** effectiveness rating at all in the material, so the "Low" from 01 §2 does not cover them | no evidence (not: evidence against) |
| AutoGen example code as production code | Prose/code discrepancies C7–C12, e.g. reflection without an iteration limit; the group chat page: "not meant to be used in real applications" | MEDIUM, directly verifiable |
| Patterns from Mastra and Medium | 0 facts (content unavailable) | no data |

## (b) Minimal chatbot architecture (agreed)
1. A JS widget (SSE) connects to an ASP.NET endpoint (proxy). The key and prompts are only on the server. Assume the system prompt will leak (LLM07).
2. Intent classification with an output in the form of a finite set of values (e.g. FAQ / order status / returns / human), and the further flow is in C# code (`switch`). **The moment of introduction is disputed, see §c.1.** The minimal version is a single model call with RAG.
3. RAG over the catalog, FAQ and policies. The retrieved content is untrusted.
4. The prompt consists of a fixed prefix (cache) and variables at the end. Cache-read tokens are monitored.
5. Reading user data (order status) exclusively through existing, authorized queries, after authentication or verification. Zero write commands. Escalation to a human is the terminal state.
6. Limits: `max_tokens`, turn limit, daily budget with a kill-switch, rate limit. The values are a **proposal** to be set.
7. A deterministic post-validator (SKU, URL, price). On a mismatch the chatbot does not send the content and falls back to a safe answer or to a human.
8. Conversation logging with personal data minimization and support for deletion.
9. No long-term memory. Session memory only as a PILOT.
10. Offline evaluation: a question set (start with approx. 50–100 questions from real tickets, prag's **proposal**), deterministic checks, an auxiliary judge from a different model family than the generator (a conclusion, not a quote).

Note from the repo (prag P2): Outbox/Inbox and sagas exist, but they do not handle LLM calls, and there is no LLM integration in the code. For a read-only v1 there is no need to build a new "deterministic shell" (journal, replay).

## (c) Preserved dissent
1. **Intent router as an enum right away (pro) or after measurement (con).** Pro wants the router in the second step. Con starts with a single call with RAG, and adds the router only when measurement justifies it. Prag stands in the middle: the router in code, without reference to Brad's pattern 3 (it has a documented "twice… does nothing" bug). No source measures the router's gain in a store chatbot.
2. **Brad's named patterns (3, 8, 9) or plain C#.** Pro wants to use them as a vocabulary. Con and prag want code without labels, because Brad's version of pattern 9 re-emits the whole artifact, and the SKU check is an ordinary post-validator.
3. **Actions with an effect (refund).** Pro allows them after implementing idempotency and a journal, with retry using the **same** key (pro corrected itself in line with bm-series-parts A6). Prag allows them only through the existing `RefundSaga` or endpoint, after a read-only pilot. Con does not propose them at all; "con rules out" is a characterization made by prag, not con directly. Common minimum: v1 is read-only.
4. **Whether directional agreement deserves STRONG.** Con and prag: no, if the independent sources (Zheng, Huang) concern a different issue than the claim. Pro stayed with STRONG directionally at #1. The judge finds the attribution of Zheng to the reducer wrong (judge-round1).

## (d) Promotion criteria and what would change the decision
All thresholds are a **proposal** (prag §c) to be calibrated on our own data.
- **Judge PILOT→ADOPT:** agreement with human labels on our own set near Zheng's reference point (approx. 80%, MT-Bench, not a store) and stability after swapping the order. Otherwise the judge stays an auxiliary signal.
- **Routing/SLM:** lower cost per conversation without a drop in quality on the evaluation set beyond a set margin. The margin is a proposal.
- **Reflection:** improvement on a set with an external validator without exceeding the latency budget.
- **Session memory:** an approved legal basis (GDPR) and no privacy incidents in the pilot.
- **Actions with an effect (DEFER→PILOT):** an idempotent turn key, retry with the same key on UNKNOWN, going through the existing saga or endpoint, escalation to a human after retries are exhausted.
- **Discipline of every promotion:** one change at a time, measurement before and after on a fixed set, rollback.
- **What would change the decision:**
  - an independent benchmark or a public Aegis repo could move the reducer to PILOT;
  - effectiveness data for debate with different models (e.g. the unread arXiv 2406.11776) could move debate to DEFER;
  - an independent benchmark of Mem0 or GEPA could move these elements to PILOT;
  - our own measurement showing that routing lowers cost without quality loss would settle §c.1.

## (e) What the evidence base does NOT say
- Nothing about an e-commerce chatbot directly. No source measures RAG, a router, a judge or limits in an online store.
- Nothing about .NET. All examples are Python, YAML and CEL.
- No independent verification of the Aegis runtime and Brad's metrics (no trials or repetitions; C5 is internally inconsistent).
- No data on the effectiveness of debate and quorum; for auction and dynamic team no rating whatsoever.
- The self-research was not cross-verified and no number in it was reproduced (README §12). Some sources are known only from abstracts or snippets.
- 5.2% of the quotes in the findings did not match the text of the sources literally. Not all of them were reviewed (01 §5).
- No data on ECommerceApp's costs and traffic. The profitability of caching, SLM and limits has to be measured.
- **The consensus is not independent confirmation.** The three debaters worked on a single base prepared by one assistant, with material dominated by one author (Brad). The agreement of positions may reproduce a shared synthesis bias, e.g. the grouping "Brad + Zheng + Huang" in 01 §2.

---

## Erratum (2026-10-14, after the review of assumptions against the code)
- **§a (ADOPT NOW, post-validators) and §b pt 7:** the wording "SKU, URL and price" is **wrong for this repo**. `ECommerceApp.Domain/Catalog/Products/Product.cs` has `Name` and `Cost` (price); it has **no SKU or URL field**; the product address is the `{id:int}` route in `StorefrontController`. The validator should check the **product Id, the route and the price**. Source: [assumption review](../../../reports/2026-10-14-agent-patterns-review-and-direction-checks.md) §3.
- **§b pt 5 (reading order status):** authorization exists (`OrderAccessAuthorizationHandler`, `VerificationCode*`), but these are MVC/cookie flows. The chatbot would have to act as a logged-in customer or a guest scoped to a single order (`GuestAccessScope`). Feasibility from a backend proxy has not been verified (hypothesis H7 in the review).
- The remaining debate files (`positions/`, `critiques/`, `judge-round1.md`) contain the same "SKU/URL/price" assumption as a historical record and were not edited.
- **File rename:** `debate/positions/con.md` was renamed to `debate/positions/position-skeptic-con.md`. The name `con` is reserved on Windows and made saving in git impossible. References such as "positions/{pro,con,prag}.md" in the text denote the positions: pro, skeptic (con), pragmatist (prag).
