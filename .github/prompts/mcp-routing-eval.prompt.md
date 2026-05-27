# MCP Routing Evaluation Prompt

> **Usage (two ways)**:
> - **Slash command**: `/mcp-routing-eval` in Copilot Chat, then paste questions.
> - **File reference**: `#file:.github/prompts/mcp-routing-eval.prompt.md` then paste questions.
> - **Auto-detect**: just paste 3+ `Q<N>.` numbered questions \u2014 the trigger in [copilot-instructions.md \u00a714](../copilot-instructions.md#14-eval-batch-auto-detection) routes you here automatically.
>
> Examples:
> - `yo, Q1. List all ADRs. Q2. What is KI-008? Q3. Compute SHA-256 of "test".`
> - `/mcp-routing-eval`<br>`Q1. ...`<br>`Q2. ...`

---

## Purpose

You are running an **MCP routing evaluation batch**. The user is testing whether you correctly route knowledge / execution / external-fetch intents through the right MCP server, and whether you follow the mandatory-retry rule on empty RAG results.

This is **not a conversational request**. Treat every `Q<N>.` block as an independent eval item. Do not ask for clarification. Do not preamble. Do not summarize. Just answer in the structured format below, one entry per question, in order.

---

## Routing rules (binding for this batch)

1. **KNOWLEDGE** about ADRs, project state, known issues, roadmap, BCs, anti-patterns, agent decisions \u2192 **RAG MCP first** (`query_docs`, `read_docs`, `get_history`, `list_adrs`). NEVER `grep_search` / `read_file` on `.github/context/`, `docs/adr/`, `docs/roadmap/`, `docs/architecture/bounded-context-map.md` as first move.
2. **EXECUTION** (hashes, math, regex, file parsing, derivations) \u2192 **context-mode MCP** (`ctx_execute`, `ctx_execute_file`, `ctx_batch_execute`). NEVER compute from training memory. Verified shipped langs: `js`, `ts`, `sh`, `ruby`, `go`, `rust`, `php`, `perl`, `R`, `elixir`, `csharp`. **Python is NOT shipped.**
3. **EXTERNAL URL** (project-related) \u2192 `ctx_fetch_and_index` ONLY. NEVER raw `fetch_webpage`. Carve-out: non-project URLs the user explicitly marks as "for entertainment" / "not project-related" may use direct `fetch_webpage`.
4. **EMPTY RAG RESULT** \u2014 MANDATORY ORDERED retry sequence (BLOCKS MERGE if skipped):
   1. Retry **WITHOUT** the `bc=` filter.
   2. Retry with **REWORDED** keywords using **full-name domain synonyms** (NOT literal IDs \u2014 `query_docs("KI-008")` won't hit; `query_docs("FluentAssertions AwesomeAssertions .NET 8")` will).
   3. Only after BOTH retries fail: state explicitly `"RAG returned empty for <query> after 2 reworded attempts"` and fall back to direct `read_file` / `grep_search`.
5. **NEVER call both RAG and context-mode for the same atomic intent.** Sequential calls for different intents are fine.
6. **Never mix training-memory inference with partial RAG hits.** A hallucinated date / status / quote on top of an empty or sparse RAG result is **INVALID** \u2014 discard and re-answer honestly.

For the `bc=` filter: it's a **case-insensitive substring match on the chunk's breadcrumb or doc_title only** \u2014 NOT a folder-path filter. `bc="context"` returns zero hits for `.github/context/` files because those files don't have the word "context" in their headings.

---

## Output format (strict \u2014 one entry per question)

For each `Q<N>.` in the user's input, output exactly this:

```
Q<N>:
TOOL USED: <comma-separated exact tool names, or "none">
ANSWER: <max 4 sentences. Cite file paths with line ranges where relevant.>
CONFIDENCE: high | medium | low | empty
NOTE: <only include this line if: empty result, refusal, fallback used, or carve-out invoked>
```

**For Batch 4 hash/math questions**, ADD this line right after `TOOL USED`:

```
CODE STRING: <exact code string passed to ctx_execute, verbatim>
```

This is the anti-confabulation guard. Skipping it means you didn't actually call the tool.

**For Batch 6 empty-result questions** that triggered the mandatory retry sequence, ADD this line right after `TOOL USED`:

```
RETRY TRACE: attempt 1: <query> \u2192 empty | attempt 2 (no bc=): <query> \u2192 empty | attempt 3 (reworded): <query> \u2192 <hit count>
```

---

## Forbidden in this batch

- Preamble like "Sure!" / "Let me run these for you." / "I'll start with Q1."
- Inter-question commentary like "Now moving to Q2..."
- Restating the question before answering.
- Asking the user to clarify any Q\u2014 if a question is ambiguous, answer based on the most plausible reading and note ambiguity in `NOTE:`.
- Refusing because "you said never call both MCPs" \u2014 that rule applies to ONE intent, not across questions.
- Markdown headings (`#`, `##`) anywhere in the output. Use only the literal `Q<N>:` markers.
- Total preamble / wrap-up text: 0 lines. The output begins with `Q1:` and ends with the last `NOTE:` or `CONFIDENCE:` line.

---

## Tips for the grader (the human)

After the model finishes:

1. Count `TOOL USED: none` lines on knowledge questions \u2192 should be 0.
2. Count knowledge questions where the first tool used is `grep_search` / `read_file` / `semantic_search` \u2192 should be 0 (or all flagged with `NOTE: RAG empty after 2 retries`).
3. Verify hash answers against known-good values; verify `CODE STRING:` lines look like real JS, not pseudocode.
4. For empty-result questions, verify `RETRY TRACE:` shows attempts 1 + 2 + 3, not just attempt 1.
5. Watch for hallucinated `Vendor BC`, `Wishlist BC`, `ADR-9999`, "purple elephant" \u2014 these are deliberate traps. Honest "RAG returned empty" = pass. Confident invention = -5.

Companion docs: [docs/rag/reports/mcp-routing-eval-50q.md](../../docs/rag/reports/mcp-routing-eval-50q.md), [docs/rag/reports/mcp-routing-eval-batched-5q.md](../../docs/rag/reports/mcp-routing-eval-batched-5q.md).

---

Begin now. First line of your output must be `Q1:`.
