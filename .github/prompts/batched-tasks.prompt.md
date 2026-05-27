# Batched Tasks Prompt \u2014 General-Purpose Structured Processing

> **Usage (three ways)**:
> - **Slash command**: `/batched-tasks` then paste a list.
> - **File reference**: `#file:.github/prompts/batched-tasks.prompt.md` then paste a list.
> - **Auto-detect**: paste any list of 3+ actionable items \u2014 the trigger in [batched-tasks.instructions.md](../instructions/batched-tasks.instructions.md) (`applyTo: **`) routes you here automatically.
>
> Examples that trigger auto-detect:
> - `please do: 1. fix the validator 2. add a test 3. update the README`
> - `yo I got tasks: - check known issues - list ADRs - compute sha256 of "x"`
> - `Q1. ... Q2. ... Q3. ...` (delegates to the stricter [mcp-routing-eval.prompt.md](mcp-routing-eval.prompt.md) when an eval keyword is also present)
> - `What's the status of Catalog? What's the status of Orders? What's the status of Sales?`

---

## Purpose

The user has pasted a **list of independent items** \u2014 questions, tasks, or mixed. Treat each item as a separate work unit. Process them in order. Output one structured block per item. No preamble. No mid-list commentary. No asking for clarification on individual items.

This prompt is the **generic** counterpart to [mcp-routing-eval.prompt.md](mcp-routing-eval.prompt.md). Use this one for normal batched work; use the eval one when the user explicitly says "eval" / "test these" / "batch test" / "score" / "measure".

---

## Binding rules (always apply, regardless of item type)

These are not optional. They come from [.github/instructions/mcp-routing.instructions.md](../instructions/mcp-routing.instructions.md) and apply to every item in the batch:

1. **KNOWLEDGE** (ADRs, project state, known issues, roadmap, BCs, anti-patterns, agent decisions) \u2192 **RAG MCP first** (`query_docs`, `read_docs`, `get_history`, `list_adrs`). NEVER `grep_search` / `read_file` on `.github/context/`, `docs/adr/`, `docs/roadmap/`, `docs/architecture/bounded-context-map.md` as first move.
2. **EXECUTION** (hashes, math, regex, file parsing, derivations) \u2192 **context-mode MCP** (`ctx_execute`, `ctx_execute_file`). NEVER compute from training memory. Verified langs: `js`, `ts`, `sh`, `ruby`, `go`, `rust`, `php`, `perl`, `R`, `elixir`, `csharp`. Python is NOT shipped.
3. **EXTERNAL URL** (project-related) \u2192 `ctx_fetch_and_index` ONLY. Carve-out: non-project URLs the user explicitly marks as "not project" may use direct `fetch_webpage`.
4. **EMPTY RAG RESULT** \u2014 MANDATORY ORDERED retry: (a) retry without `bc=`, (b) retry with reworded full-name synonyms (not literal IDs). Only after BOTH fail may you report empty.
5. **NEVER call both RAG and context-mode for the same atomic intent.** Sequential calls for different intents are fine.
6. **CODE EDITS** \u2014 if an item asks you to edit a file, use the appropriate edit tool. Do not skip to a textual diff in the output unless the user explicitly asked for a diff.

---

## Output format (adaptive)

**Match the user's input style.** Detect the prefix pattern in the input and mirror it in the output:

| User input style                              | Output prefix per item |
| --------------------------------------------- | ---------------------- |
| `Q1.` `Q2.` `Q3.`                             | `Q1:` `Q2:` `Q3:`      |
| `1.` `2.` `3.` (numbered)                     | `1:` `2:` `3:`         |
| `- ...` `- ...` `- ...` (bullets)             | `- Item 1:` `- Item 2:` ... |
| `Task 1:` `Task 2:` `Task 3:`                 | `Task 1:` `Task 2:` ...|
| Multiple `?`-ending sentences in one paragraph| `1:` `2:` `3:` (fall back to numbered) |
| `* ...` `* ...` `* ...`                       | `* Item 1:` `* Item 2:`|

For each item, output:

```
<prefix>
TOOL USED: <comma-separated exact tool names, or "none">
ANSWER: <max 4 sentences. Cite file paths with line ranges where relevant.>
CONFIDENCE: high | medium | low | empty
NOTE: <only if: empty result, refusal, fallback used, carve-out invoked, ambiguity in the item, or edit was performed>
```

**Compact mode** (when the user explicitly says "fast" / "quick" / "short" / "no metadata"):

```
<prefix> <answer in 1-2 sentences>
```

Skip `TOOL USED` / `CONFIDENCE` / `NOTE` in compact mode. The MCP routing rules still apply, you just don't surface them.

---

## Forbidden in batch mode

- Preamble like "Sure!" / "Let me work through these." / "I'll start with the first one."
- Mid-list commentary like "Now moving to item 2..." or "That was straightforward, next:"
- Restating each item before answering.
- Asking the user to clarify individual items \u2014 use `NOTE: ambiguity:` and answer the most plausible reading.
- Markdown headings (`#`, `##`) anywhere in the output. Use only the prefix markers from the table above.
- Total preamble / wrap-up text: 0 lines. Output begins with the first prefix and ends with the last item's last line.
- TODO list creation in the output (use the actual todo tool if needed, but don't paste a markdown TODO into the answer).

---

## Edge cases

**Mixed item types.** If the batch mixes knowledge questions, code edits, and execution requests, handle each per its own routing rule. Output blocks remain uniform.

**Dependencies between items.** If item N depends on item N-1 (e.g. "5. now run that snippet on file X"), process sequentially. State the dependency in `NOTE:` of item N.

**Single-item ambiguity.** If exactly one item is ambiguous, answer the most plausible reading and note in `NOTE:`. Do not block the whole batch on one ambiguity.

**Whole-batch ambiguity** (you can't tell what the user wants at all). One short clarification question is allowed BEFORE you start, not in the middle.

**Long-running edits.** If an item requires a multi-step file edit (e.g. "refactor X across 12 files"), do the edits, then in the output put `ANSWER: edited <count> files: <comma-separated paths>`. Don't paste full diffs in batch mode.

**Code execution items.** Always include the `CODE STRING:` line right after `TOOL USED:` for math/hash/parse items, even in non-eval mode. This is anti-confabulation insurance.

---

## When this prompt is NOT the right tool

- **Single conversational question** (1 item) \u2014 just answer normally.
- **Long-form essay request** (\"write me a doc about X\") \u2014 use normal markdown.
- **Genuine multi-turn troubleshooting** where you need clarification between steps \u2014 stay conversational.
- **Eval / scoring run** with `Q<N>.` markers AND the user said \"eval\" / \"test\" / \"score\" / \"batch test\" \u2014 use [mcp-routing-eval.prompt.md](mcp-routing-eval.prompt.md) instead (stricter rules, includes `RETRY TRACE:` and grading guidance).

---

Begin now. First line of output must be the first item's prefix. No preamble.
