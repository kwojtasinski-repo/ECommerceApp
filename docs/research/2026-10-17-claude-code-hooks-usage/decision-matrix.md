# Decision matrix — mechanism vs rule type

Sources: [findings.md](findings.md). Codes such as D3.3 refer to the evidence tables in `analysis/findings/`. "Synthesis" marks cells derived by combining sources, not quoted.

## 1. Mechanisms compared

| Property | Instruction / CLAUDE.md / `*.instructions.md` | Skill | Permission rules | Hook (command) | Hook (prompt/agent) | CI pipeline | pre-commit (git) |
|---|---|---|---|---|---|---|---|
| Enforced deterministically? | No: model may ignore it (D3.3) | No: model must invoke or follow it | Yes (D1.8, D3.3) | Yes, at the event (D2.1) | Trigger yes, verdict is model judgment (D3.5) | Yes | Yes, unless bypassed with `--no-verify` (not verified from docs) |
| Works in Claude **and** Copilot from one file? | Partly (separate files, same text possible) | Partly (both have skills) | No (each tool has its own permission system) | Only with per-tool adapters (V3) | Claude-specific types; Copilot has `prompt` only on `sessionStart` (D6) | Yes, tool-agnostic | Yes, tool-agnostic |
| Catches humans too? | No | No | No | No | No | **Yes** | Yes (local) |
| When it acts | When the model reads it | When invoked | Before a tool call | At a lifecycle event | At a lifecycle event | After push/PR | At commit |
| Context cost | High if always loaded (D3 table) | Low until invoked | None | Low (D3.5) | Separate model call | None | None |
| Latency cost per tool call | None | None | ~None | Yes, synchronous (B1, D1.5) | Highest (model call) | None in session | None in session |
| Where the rule is visible | In the text | In the skill | Settings | In code; better if rule data lives in config (I11, R1) | In the prompt text | Pipeline file | Hook config |
| Failure mode if broken | Rule silently ignored | Skill not loaded | Rule not matched | Claude: proceeds by default; Copilot `preToolUse`: denies on crash; both proceed on timeout (V3) | As command hooks + model error | Build red | Commit blocked or skipped |
| Can understand intent? | Yes (model) | Yes (model) | No | No (text only) | Partly (model) | No | No |

## 2. Rule type → recommended mechanism

| Rule type | Example | 1st choice | Add-on layer | Avoid | Basis |
|---|---|---|---|---|---|
| Static convention, style, context | "UI labels stay in Polish" | Instruction | — | Hook | D1.9, D3 |
| Multi-step procedure | Research workflow, deploy checklist | Skill | Instruction pointer | Hook | D3.4 |
| Heuristic with an evolving trigger list | `linq-complexity-discovery` | Skill + instruction | Optional advisory hook (context injection, no deny) with triggers kept in a data file | Blocking hook with triggers hard-coded in hook code | V4/H2; R1 pattern; X2 friction |
| Hard allow/deny on a tool + argument pattern | Never read `.env`; never `git push --force` | **Permission rules** | `PreToolUse` hook when the rule needs code (parsing, content) | Instruction only | D1.8, D3.3, D2.5 |
| Invariant needing state or human consent | GO gate (edits locked until `IMPLEMENT`) | `PreToolUse` + `UserPromptSubmit` hook | README + instruction explaining it; adapters per tool | Instruction only (ADR-0033 rejected it) | I14, D3.3 |
| "Always run X after Y" | Format after edit | `PostToolUse` hook (async if non-blocking) | CI check as backstop | Instruction | D3.2, B1 |
| Must also bind humans / every commit | Tests pass, lint clean, no secrets committed | CI | pre-commit; hook as early feedback inside the session | Hook only | Synthesis. CI/pre-commit criteria **not verified** from sources (98-rejected) |
| Org-wide non-negotiable policy | Block credential access fleet-wide | Managed settings (Claude) / policy hooks (Copilot CLI) | — | Project-level hook only | D3.3, D6.1 |
| Needs judgment *and* must be enforced | TDD order | Prompt/agent hook (Claude) or LLM inside a command hook (R2) | Skill explaining the method | Regex hook | D2.4, R2. Agent hooks are experimental (D1.2) |
| Observability / audit | Log tool calls | Async `PostToolUse` / HTTP hook | — | Synchronous blocking hook | D1, B1 §7.3 |

## 3. Quick checklist before adding a hook

Score each item. A hook is justified when items 1–4 are all "yes" and item 5 is answered.

1. **Mechanical?** Can the rule be decided from the payload text (tool name, path, prompt prefix) without understanding intent? (H1, D1.8)
2. **Costly miss?** Is a miss expensive or irreversible enough that "the model usually complies" is not acceptable? (D3.3)
3. **Fast?** Does it reliably finish in well under the tool's timeout, with an explicit `timeout` set? (D1.5, B1)
4. **Not already expressible as a permission rule?** If it is, use the rule. (D1.8)
5. **Failure mode chosen?** Is fail-open or fail-closed decided and written down per tool, given that the defaults differ (V3)? Is there a human escape hatch, a test, and an inventory entry? (Q3)

If the rule must hold in both tools: add an adapter per tool (V3), and put the explanation in shared instructions so the model understands the denials.
