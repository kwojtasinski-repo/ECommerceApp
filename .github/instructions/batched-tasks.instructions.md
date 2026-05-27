---
applyTo: "**"
---

# Batched-tasks auto-detection

If the user's message contains a **list of 3 or more discrete actionable items** — questions, tasks, or mixed — treat it as a **batched task list** and apply the rules + adaptive structured output format from [.github/prompts/batched-tasks.prompt.md](../prompts/batched-tasks.prompt.md) automatically. Do NOT preamble. Do NOT ask for confirmation. Output begins with the first item's prefix.

## Detection patterns (any of these triggers batch mode)

| Pattern                                            | Example                                          |
| -------------------------------------------------- | ------------------------------------------------ |
| 3+ `Q<N>.` markers                                 | `Q1. ... Q2. ... Q3. ...`                        |
| 3+ numbered items (`1.` `2.` `3.` or `1)` `2)` ...) | `1. fix the validator 2. add a test 3. ...`     |
| 3+ bulleted items (`-` or `*`)                     | `- check KI-008\n- list ADRs\n- compute sha256` |
| 3+ `Task <N>:` / `Step <N>:` prefixes              | `Task 1: ... Task 2: ... Task 3: ...`            |
| 3+ separate `?`-ending sentences in one message    | `What is X? What is Y? What is Z?`               |

## Eval-mode delegation

If the input has `Q<N>.` markers AND the user said any of "eval" / "test these" / "batch test" / "score" / "measure" / "rate" / "grade", delegate to the stricter [.github/prompts/mcp-routing-eval.prompt.md](../prompts/mcp-routing-eval.prompt.md) instead (adds `CODE STRING:` and `RETRY TRACE:` requirements and forbids markdown headings entirely).

## Output shape

Adapts to input style:

- `Q<N>.` input → `Q<N>:` output blocks
- Numbered input → `<N>:` blocks
- Bulleted input → `- Item <N>:` blocks

Per-item shape: `TOOL USED:` / `ANSWER:` / `CONFIDENCE:` / optional `NOTE:`. Full table in the prompt file.

## Compact mode

If the user adds "fast" / "quick" / "short" / "no metadata" to the message, output one line per item (`<prefix> <answer>`), skipping the metadata fields. MCP routing rules still apply silently.

## Negative triggers (do NOT activate batch mode)

- Fewer than 3 items.
- A single question that happens to contain a numbered example list inside it.
- A long-form essay / documentation request.
- Genuine multi-turn troubleshooting where each step depends on the previous response.

Full rules, edge cases, and forbidden patterns: [.github/prompts/batched-tasks.prompt.md](../prompts/batched-tasks.prompt.md).
